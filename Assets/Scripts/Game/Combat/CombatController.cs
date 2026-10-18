using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Flow;
using CC.Core.Grid;
using CC.Core.Run;
using CC.Core.Util;
using CC.Data;
using CC.Game.Boot;
using CC.Game.Input;
using CC.Game.Stage;
using UnityEngine;

namespace CC.Game.Combat
{
    /// <summary>
    /// The combat view (rudimentary until the UI milestone). Builds a CombatSession when the flow enters
    /// Combat, shows it as an overlay on the stage scene (same scene, Konzept 4.1), plays back the
    /// events the session returns and passes the player's menu choices on. Contains no rules.
    /// </summary>
    public sealed class CombatController : MonoBehaviour
    {
        private enum Mode
        {
            Idle,
            Playing,
            Menu,
            Target,
            Result,
            GameOver
        }

        private sealed class Actor
        {
            public EntityView View;
            public Vector3 Home;
        }

        private struct FloatingText
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
        }

        private const int BackdropOrder = 100;
        private const int ActorOrder = 101;
        private const int MaxLogLines = 7;
        private const float FloatSeconds = 1.0f;

        [SerializeField] private StageController stage;
        [Tooltip("Seconds one action takes on screen. 0 plays everything instantly (used by tests).")]
        [SerializeField] private float actionSeconds = 0.55f;

        private GameFlow _flow;
        private InputRouter _input;
        private Mode _mode = Mode.Idle;

        private CombatSession _session;
        private EnemySpec _enemySpec;
        private Combatant _enemy;
        private int _stageEnemyId = -1;
        private int _pendingEnemyId = -1;

        private Transform _root;
        private readonly Dictionary<Combatant, Actor> _actors = new Dictionary<Combatant, Actor>();
        private readonly Dictionary<Combatant, int> _shownHp = new Dictionary<Combatant, int>();
        private readonly List<string> _log = new List<string>();
        private readonly List<FloatingText> _floating = new List<FloatingText>();
        private Combatant _active;
        private int _chain;
        private float _chainBannerUntil;
        private string _chainBanner = "";

        private int _menuIndex;
        private int _targetIndex;
        private Skill _pendingSkill;
        private List<Combatant> _targets = new List<Combatant>();

        private string _resultTitle = "";
        private readonly List<string> _resultLines = new List<string>();

        private GUIStyle _label;
        private GUIStyle _big;
        private Texture2D _white;

        /// <summary>True when everything is shown without waiting. Used by tests so a fight resolves in one frame.</summary>
        private bool Instant => actionSeconds <= 0f;

        /// <summary>True while the combat overlay is up. Read by the debug overlay.</summary>
        public bool InCombat => _mode != Mode.Idle;
        public CombatSession Session => _session;

        private void OnEnable()
        {
            _flow = Services.Get<GameFlow>();
            _input = Services.Get<InputRouter>();
            GameEvents.EnemyEngaged += OnEngaged;
            GameEvents.FlowStateChanged += OnFlowStateChanged;
            _input.NavigateRequested += OnNavigate;
            _input.ConfirmPressed += OnConfirm;
            _input.CancelPressed += OnCancel;
        }

        private void OnDisable()
        {
            GameEvents.EnemyEngaged -= OnEngaged;
            GameEvents.FlowStateChanged -= OnFlowStateChanged;
            if (_input == null) return;
            _input.NavigateRequested -= OnNavigate;
            _input.ConfirmPressed -= OnConfirm;
            _input.CancelPressed -= OnCancel;
        }

        // ------------------------------------------------------------ flow

        private void OnEngaged(int stageEnemyId) => _pendingEnemyId = stageEnemyId;

        private void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            if (next == GameFlowState.Combat && _pendingEnemyId >= 0)
            {
                int id = _pendingEnemyId;
                _pendingEnemyId = -1;
                BeginCombat(id);
            }
        }

        private void BeginCombat(int stageEnemyId)
        {
            _stageEnemyId = stageEnemyId;
            var run = stage.Run;
            var content = stage.Content;
            var stageEnemy = stage.Session.EnemyById(stageEnemyId);

            var party = new List<Combatant>();
            for (int i = 0; i < run.Party.Members.Count; i++)
                party.Add(Combatant.ForMember(i + 1, run.Party.Members[i]));

            var rng = new SeededRng(run.Seed).Derive(3000 + run.StageNumber * 100 + stageEnemyId);
            _enemySpec = content.EnemySpecFor(stageEnemy.TypeId);
            _enemy = _enemySpec.CreateCombatant(100, stageEnemy.Level, rng.Derive(1));

            _session = new CombatSession(party, new[] { _enemy }, rng,
                new ChainResolver(content.CreateChainConfig()), content.fleeChance);

            _log.Clear();
            _floating.Clear();
            _resultLines.Clear();
            _chain = 0;
            _chainBannerUntil = 0f;
            _active = null;
            BuildActors(content);

            foreach (var c in party.Concat(new[] { _enemy })) _shownHp[c] = c.Hp;

            var events = _session.Start();
            StartCoroutine(Play(events));
        }

        // ------------------------------------------------------------ playback

        private IEnumerator Play(IReadOnlyList<CombatEvent> events)
        {
            _mode = Mode.Playing;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case CombatEventKind.TurnStarted:
                        _active = e.Actor;
                        break;

                    case CombatEventKind.SkillUsed:
                        if (Instant) ApplyStrike(e);
                        else yield return Strike(e);
                        break;

                    case CombatEventKind.StatusApplied:
                        AddLog($"{e.Target.Name} is {StatusName(e.StatusId)}");
                        if (!Instant) yield return Wait(0.2f);
                        break;

                    case CombatEventKind.StatusExpired:
                        AddLog($"{e.Target.Name} is no longer {StatusName(e.StatusId)}");
                        break;

                    case CombatEventKind.Defeated:
                        AddLog($"{e.Target.Name} is defeated");
                        SetAlpha(e.Target, 0.25f);
                        if (!Instant) yield return Wait(0.3f);
                        break;

                    case CombatEventKind.ChainEnded:
                        if (e.ChainIndex > 1)
                        {
                            _chainBanner = $"CHAIN x{e.ChainIndex}!";
                            _chainBannerUntil = Time.unscaledTime + 1.2f;
                            AddLog($"Chain of {e.ChainIndex}");
                            if (!Instant) yield return Wait(0.35f);
                        }
                        break;

                    case CombatEventKind.FleeFailed:
                        AddLog($"{e.Actor.Name} could not get away");
                        if (!Instant) yield return Wait(0.3f);
                        break;

                    case CombatEventKind.Fled:
                        AddLog($"{e.Actor.Name} escapes");
                        break;
                }
            }

            _chain = 0;
            FinishPlayback();
        }

        /// <summary>The part of a skill use that changes what is shown: log, hit points, floating number, chain count.</summary>
        private void ApplyStrike(CombatEvent e)
        {
            _chain = e.ChainIndex;
            AddLog($"{e.Actor.Name} uses {e.Skill.Name}" + (e.IsFollowUp ? " (chain)" : "") +
                   (e.Actor == e.Target ? "" : $" on {e.Target.Name}"));

            _shownHp[e.Target] = e.TargetHpAfter;
            bool heal = e.Skill.Kind == SkillKind.Heal;
            _floating.Add(new FloatingText
            {
                World = ActorHome(e.Target) + new Vector3(0f, 1.1f, 0f),
                Text = (heal ? "+" : "-") + e.Amount,
                Color = heal ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.35f),
                Born = Time.unscaledTime
            });
        }

        private Vector3 ActorHome(Combatant who) => _actors.TryGetValue(who, out var a) ? a.Home : Vector3.zero;

        /// <summary>Lunge at the target, show the hit, return. Only used when time passes on screen.</summary>
        private IEnumerator Strike(CombatEvent e)
        {
            var from = _actors[e.Actor];
            var to = _actors[e.Target];

            float leg = actionSeconds * 0.22f;
            Vector3 attackPos = Vector3.Lerp(from.Home, to.Home, e.Actor == e.Target ? 0f : 0.55f);
            yield return MoveActor(from, from.Home, attackPos, leg);

            ApplyStrike(e);
            if (e.Skill.Kind != SkillKind.Heal) yield return Flash(e.Target, actionSeconds * 0.12f);

            yield return MoveActor(from, attackPos, from.Home, leg);
            yield return Wait(actionSeconds * 0.3f);
        }

        private IEnumerator MoveActor(Actor actor, Vector3 a, Vector3 b, float seconds)
        {
            if (seconds <= 0f)
            {
                actor.View.transform.position = b;
                yield break;
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                actor.View.transform.position = Vector3.Lerp(a, b, t / seconds);
                yield return null;
            }
            actor.View.transform.position = b;
        }

        private IEnumerator Flash(Combatant target, float seconds)
        {
            if (!_actors.TryGetValue(target, out var actor)) yield break;
            var renderer = actor.View.Renderer;
            var original = renderer.color;
            renderer.color = new Color(1f, 0.3f, 0.3f, original.a);
            yield return Wait(seconds);
            if (renderer != null) renderer.color = original;
        }

        private IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f || actionSeconds <= 0f) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        private void SetAlpha(Combatant who, float alpha)
        {
            if (!_actors.TryGetValue(who, out var actor)) return;
            var c = actor.View.Renderer.color;
            actor.View.Renderer.color = new Color(c.r, c.g, c.b, alpha);
        }

        private void FinishPlayback()
        {
            _active = null;
            if (_session.Outcome != CombatOutcome.None)
            {
                ShowResult();
                return;
            }

            _active = _session.Current;
            _menuIndex = 0;
            _mode = Mode.Menu;
        }

        private void ShowResult()
        {
            _mode = Mode.Result;
            _resultLines.Clear();

            switch (_session.Outcome)
            {
                case CombatOutcome.Won:
                    _resultTitle = "VICTORY";
                    int xp = _enemySpec.XpReward(_enemy.Level);
                    _resultLines.Add($"{_enemy.Name} (level {_enemy.Level}) defeated. +{xp} XP for everyone still standing.");
                    foreach (var member in stage.Run.Party.Members.Where(m => m.Hp > 0))
                    {
                        foreach (int level in member.GainXp(xp))
                            _resultLines.Add($"{member.Name} reached level {level}!");
                    }
                    break;

                case CombatOutcome.Fled:
                    _resultTitle = "ESCAPED";
                    _resultLines.Add($"The party got away from {_enemy.Name}.");
                    break;

                default:
                    _resultTitle = "DEFEAT";
                    _resultLines.Add($"The party fell on stage {stage.StageNumber}. The run is over.");
                    break;
            }
        }

        // ------------------------------------------------------------ input

        private void OnNavigate(Vector2Int direction)
        {
            if (_flow.Current != GameFlowState.Combat) return;

            int step = direction.y > 0 ? -1 : direction.y < 0 ? 1 : 0;
            if (step == 0) return;

            if (_mode == Mode.Menu)
            {
                int count = MenuEntryCount();
                _menuIndex = (_menuIndex + step + count) % count;
            }
            else if (_mode == Mode.Target && _targets.Count > 0)
            {
                _targetIndex = (_targetIndex + step + _targets.Count) % _targets.Count;
            }
        }

        private void OnConfirm()
        {
            if (_mode == Mode.GameOver && _flow.Current == GameFlowState.GameOver)
            {
                _mode = Mode.Idle;
                Teardown();
                _flow.ReturnToHub();
                return;
            }

            if (_flow.Current != GameFlowState.Combat) return;

            switch (_mode)
            {
                case Mode.Menu:
                    ConfirmMenu();
                    break;
                case Mode.Target:
                    Submit(_pendingSkill, _targets[_targetIndex]);
                    break;
                case Mode.Result:
                    ConfirmResult();
                    break;
            }
        }

        private void OnCancel()
        {
            if (_flow.Current == GameFlowState.Combat && _mode == Mode.Target) _mode = Mode.Menu;
        }

        private int MenuEntryCount() => (_session?.Current?.Skills.Count ?? 0) + 1; // + flee

        private void ConfirmMenu()
        {
            var actor = _session.Current;
            if (actor == null) return;

            if (_menuIndex >= actor.Skills.Count)
            {
                StartCoroutine(Play(_session.TryFlee()));
                return;
            }

            var skill = actor.Skills[_menuIndex];
            var targets = _session.ValidTargets(actor, skill).ToList();
            if (targets.Count == 0) return;

            if (targets.Count == 1 || skill.Target == SkillTarget.Enemy)
            {
                Submit(skill, targets[0]);
                return;
            }

            _pendingSkill = skill;
            _targets = targets;
            _targetIndex = Mathf.Max(0, targets.IndexOf(actor));
            _mode = Mode.Target;
        }

        private void Submit(Skill skill, Combatant target) =>
            StartCoroutine(Play(_session.SubmitAction(skill, target)));

        private void ConfirmResult()
        {
            if (_session.Outcome == CombatOutcome.Lost)
            {
                _mode = Mode.GameOver;
                _flow.GoTo(GameFlowState.GameOver);
                return;
            }

            stage.ResolveEngagement(_stageEnemyId);
            _stageEnemyId = -1;
            _mode = Mode.Idle;
            Teardown();
            _flow.GoTo(GameFlowState.Exploring);
        }

        // ------------------------------------------------------------ visuals

        private void BuildActors(GameContent content)
        {
            Teardown();
            var cam = Camera.main;
            if (cam == null) return;

            _root = new GameObject("CombatView").transform;
            _root.SetParent(cam.transform, false);
            _root.localPosition = new Vector3(0f, 0f, 10f);

            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;

            var backdrop = new GameObject("Backdrop", typeof(SpriteRenderer));
            backdrop.transform.SetParent(_root, false);
            var sr = backdrop.GetComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = new Color(0.07f, 0.07f, 0.12f, 1f);
            sr.sortingOrder = BackdropOrder;
            backdrop.transform.localScale = new Vector3(width, height, 1f);

            var party = _session.Party;
            for (int i = 0; i < party.Count; i++)
            {
                var combatant = party[i];
                var def = content.MemberDefFor(combatant.TypeId);
                var view = EntityView.Create<PlayerView>(combatant.Name, _root, def != null ? def.animator : content.playerAnimator, ActorOrder);
                if (def != null) view.Renderer.color = def.tint;
                view.Face(Direction.Right);

                float y = (party.Count - 1) * 0.5f * 1.6f - i * 1.6f;
                var home = _root.position + new Vector3(-width * 0.28f, y - 0.2f, 0f);
                view.transform.localScale = Vector3.one * 2f;
                view.transform.position = home;
                _actors[combatant] = new Actor { View = view, Home = home };
                if (combatant.Hp <= 0) SetAlpha(combatant, 0.25f);
            }

            var enemyDef = content.BiomeFor(stage.StageNumber).FindEnemy(_enemy.TypeId);
            var enemyView = EntityView.Create<EnemyView>(_enemy.Name, _root,
                enemyDef != null ? enemyDef.animator : null, ActorOrder);
            enemyView.transform.localScale = Vector3.one * 3f;
            var enemyHome = _root.position + new Vector3(width * 0.26f, 0.2f, 0f);
            enemyView.transform.position = enemyHome;
            _actors[_enemy] = new Actor { View = enemyView, Home = enemyHome };
        }

        private Sprite WhiteSprite()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            return Sprite.Create(_white, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void Teardown()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _actors.Clear();
            _shownHp.Clear();
            _floating.Clear();
        }

        private void OnDestroy()
        {
            if (_white != null) Destroy(_white);
        }

        // ------------------------------------------------------------ IMGUI

        private void AddLog(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        private string StatusName(string id) => id switch
        {
            "bleed" => "bleeding",
            "burn" => "burning",
            "shaken" => "shaken",
            _ => id
        };

        private void OnGUI()
        {
            if (_mode == Mode.Idle || _session == null) return;

            EnsureStyles();
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            const float h = 720f;

            // Enemy panel, top right
            DrawCombatant(new Rect(w - 360f, 24f, 336f, 74f), _enemy, false);

            // Party panel, left
            float y = 24f;
            foreach (var member in _session.Party)
            {
                DrawCombatant(new Rect(24f, y, 336f, 74f), member, member == _active && _mode != Mode.Result);
                y += 82f;
            }

            // Header
            GUI.Label(new Rect(w / 2f - 160f, 12f, 320f, 28f),
                $"Round {_session.Round}    Chain {Mathf.Max(_chain, 0)}/{_session.ChainConfig.Max}", _label);
            if (Time.unscaledTime < _chainBannerUntil)
                GUI.Label(new Rect(w / 2f - 200f, 150f, 400f, 60f), _chainBanner, _big);

            DrawLog(new Rect(w - 420f, h - 190f, 396f, 170f));

            if (_mode == Mode.Menu) DrawMenu(new Rect(24f, h - 200f, 420f, 180f));
            if (_mode == Mode.Target) DrawTargets(new Rect(24f, h - 200f, 420f, 180f));
            if (_mode == Mode.Result) DrawResult(new Rect(w / 2f - 260f, 240f, 520f, 220f));
            if (_mode == Mode.GameOver)
                DrawBox(new Rect(w / 2f - 260f, 240f, 520f, 140f),
                    $"GAME OVER\nReached stage {stage.StageNumber}\n\nEnter / E / Space: back to the hub");

            DrawFloating();
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawCombatant(Rect r, Combatant c, bool active)
        {
            GUI.color = active ? new Color(1f, 0.95f, 0.5f) : Color.white;
            GUI.Box(r, GUIContent.none);
            GUI.color = Color.white;

            int shown = _shownHp.TryGetValue(c, out var hp) ? hp : c.Hp;
            string statuses = c.Statuses.Count == 0 ? "" : "  [" + string.Join(", ", c.Statuses.Select(x => StatusName(x.Id))) + "]";
            GUI.Label(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 24f),
                $"{(active ? "> " : "")}{c.Name}  Lv {c.Level}{statuses}", _label);

            var bar = new Rect(r.x + 8f, r.y + 32f, r.width - 16f, 14f);
            DrawBar(bar, c.Stats.MaxHp > 0 ? (float)shown / c.Stats.MaxHp : 0f,
                shown * 100 < c.Stats.MaxHp * 30 ? new Color(0.9f, 0.3f, 0.25f) : new Color(0.35f, 0.8f, 0.4f));
            GUI.Label(new Rect(r.x + 8f, r.y + 48f, r.width - 16f, 22f),
                $"HP {shown}/{c.Stats.MaxHp}   ATK {c.Stats.Attack}  DEF {c.Stats.Defense}  SPD {c.Stats.Speed}", _label);
        }

        private void DrawBar(Rect r, float fraction, Color fill)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawMenu(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            var actor = _session.Current;
            if (actor == null) return;

            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 24f), $"{actor.Name}: choose an action", _label);
            float y = r.y + 34f;
            for (int i = 0; i < actor.Skills.Count; i++)
            {
                var skill = actor.Skills[i];
                string detail = skill.Kind == SkillKind.Heal ? "heal" : $"power {skill.Power}";
                if (skill.AppliesStatus != null) detail += $", {StatusName(skill.AppliesStatus)}";
                if (skill.FollowUpOn != null) detail += $", follows {StatusName(skill.FollowUpOn)}";
                GUI.Label(new Rect(r.x + 10f, y, r.width - 20f, 24f),
                    $"{(i == _menuIndex ? ">" : " ")} {skill.Name}   ({detail})", _label);
                y += 26f;
            }
            GUI.Label(new Rect(r.x + 10f, y, r.width - 20f, 24f),
                $"{(_menuIndex == actor.Skills.Count ? ">" : " ")} Flee", _label);
        }

        private void DrawTargets(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 24f),
                $"{_pendingSkill.Name}: choose a target   (Esc: back)", _label);
            float y = r.y + 34f;
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                GUI.Label(new Rect(r.x + 10f, y, r.width - 20f, 24f),
                    $"{(i == _targetIndex ? ">" : " ")} {t.Name}  {t.Hp}/{t.Stats.MaxHp}", _label);
                y += 26f;
            }
        }

        private void DrawLog(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            float y = r.y + 6f;
            foreach (var line in _log)
            {
                GUI.Label(new Rect(r.x + 8f, y, r.width - 16f, 22f), line, _label);
                y += 22f;
            }
        }

        private void DrawResult(Rect r)
        {
            DrawBox(r, _resultTitle + "\n\n" + string.Join("\n", _resultLines) + "\n\nEnter / E / Space: continue");
        }

        private void DrawBox(Rect r, string text)
        {
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, r.height - 20f), text, _label);
        }

        private void DrawFloating()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float s = Screen.height / 720f;

            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                var f = _floating[i];
                float age = Time.unscaledTime - f.Born;
                if (age > FloatSeconds) { _floating.RemoveAt(i); continue; }

                var screen = cam.WorldToScreenPoint(f.World + new Vector3(0f, age * 0.8f, 0f));
                var rect = new Rect(screen.x / s - 40f, (Screen.height - screen.y) / s - 16f, 80f, 32f);
                GUI.color = new Color(f.Color.r, f.Color.g, f.Color.b, 1f - age / FloatSeconds);
                GUI.Label(rect, f.Text, _big);
            }
            GUI.color = Color.white;
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = false, wordWrap = true };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
