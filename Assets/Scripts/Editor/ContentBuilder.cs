using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CC.Editor
{
    /// <summary>
    /// Builds the design-data assets (animator controllers, enemy and biome definitions, GameContent)
    /// from the baseline art in Assets/Export. Re-runnable: existing assets are updated in place,
    /// so references to them stay valid. Menu: Chaining Chronicle/Build Content.
    /// </summary>
    public static class ContentBuilder
    {
        private const string ContentRoot = "Assets/Content";
        private const string Export = "Assets/Export";

        /// <summary>How a baseline clip is turned into the clip that is used in a controller.</summary>
        private enum ClipKind
        {
            /// <summary>Same frames and timing as the baseline clip.</summary>
            Baseline,
            /// <summary>Walk loop: frames spaced evenly over exactly one second, so the controller can set the speed.</summary>
            WalkCycle,
            /// <summary>One standing pose: the first frame of the baseline clip.</summary>
            Still
        }

        private readonly struct StateSpec
        {
            public readonly string Name, SourcePath, ClipName;
            public readonly ClipKind Kind;

            public StateSpec(string name, string sourcePath, ClipKind kind = ClipKind.Baseline)
            {
                Name = name;
                SourcePath = sourcePath;
                Kind = kind;
                ClipName = kind == ClipKind.Still ? name : System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            }
        }

        // Speed multiplier parameter of the walk states. Set at runtime so one walk cycle spans a fixed number of steps.
        public const string WalkSpeedParameter = "walkSpeed";

        // Frame rate of the clips built here. Unity extends a sprite clip by one frame past its last key.
        private const float BuiltClipFrameRate = 60f;

        private sealed class EnemyEntry
        {
            public string Id, DisplayName, Clip;
            public int FirstStage;
            // Level 1 values. Placeholders without GDD basis (A25), balancing comes in the final milestone.
            public int Hp, Attack, Defense, Speed, BaseXp;
            public EnemyAi Ai = EnemyAi.Fsm;
            public string[] Skills;
        }

        private static readonly EnemyEntry[] Enemies =
        {
            new EnemyEntry { Id = "mouse",        DisplayName = "Mouse",         Clip = "Enemies/mouse/mouse.anim",              FirstStage = 1,
                             Hp = 14, Attack = 3, Defense = 0, Speed = 12, BaseXp = 8,  Ai = EnemyAi.Random, Skills = new[] { "bite" } },
            new EnemyEntry { Id = "hooddude",     DisplayName = "Hood Dude",     Clip = "Enemies/HoodDude/HoodDude.anim",        FirstStage = 2,
                             Hp = 20, Attack = 4, Defense = 1, Speed = 9,  BaseXp = 10, Skills = new[] { "stab", "mend" } },
            new EnemyEntry { Id = "evileye",      DisplayName = "Evil Eye",      Clip = "Enemies/evilEye/evilEye.anim",          FirstStage = 3,
                             Hp = 18, Attack = 5, Defense = 0, Speed = 8,  BaseXp = 12, Skills = new[] { "gaze", "burst" } },
            new EnemyEntry { Id = "walrus",       DisplayName = "Walrus",        Clip = "Enemies/walrus/walrus.anim",            FirstStage = 4,
                             Hp = 38, Attack = 5, Defense = 3, Speed = 5,  BaseXp = 14, Skills = new[] { "tusk" } },
            new EnemyEntry { Id = "livingarmour", DisplayName = "Living Armour", Clip = "Enemies/LivingArmour/LivingAmour.anim", FirstStage = 5,
                             Hp = 34, Attack = 6, Defense = 5, Speed = 4,  BaseXp = 14, Skills = new[] { "slam" } },
            new EnemyEntry { Id = "magmaegg",     DisplayName = "Magma Egg",     Clip = "Enemies/magmaegg/magmaegg.anim",        FirstStage = 7,
                             Hp = 28, Attack = 8, Defense = 2, Speed = 7,  BaseXp = 16, Skills = new[] { "scorch", "mend" } },
        };

        private static readonly (string id, string name, Color color)[] StatusTable =
        {
            ("bleed", "Bleeding", new Color(0.85f, 0.2f, 0.2f)),
            ("burn", "Burning", new Color(0.95f, 0.55f, 0.1f)),
            ("shaken", "Shaken", new Color(0.8f, 0.75f, 0.3f)),
        };

        // id, name, power, target, kind, applies, followUpOn. The three chain skills close a cycle
        // (bleed -> burn -> shaken -> bleed), the upper limit of the chain is what stops it.
        private static readonly (string id, string name, int power, SkillTarget target, SkillKind kind, string applies, string followUp)[] SkillTable =
        {
            ("slash",        "Slash",        4, SkillTarget.Enemy, SkillKind.Damage, "bleed",  null),
            ("mend",         "Mend",         6, SkillTarget.Ally,  SkillKind.Heal,   null,     null),
            ("flame_pillar", "Flame Pillar", 6, SkillTarget.Enemy, SkillKind.Damage, "burn",   "bleed"),
            ("fissure",      "Fissure",      5, SkillTarget.Enemy, SkillKind.Damage, "shaken", "burn"),
            ("rend",         "Rend",         4, SkillTarget.Enemy, SkillKind.Damage, "bleed",  "shaken"),
            ("bite",         "Bite",         2, SkillTarget.Enemy, SkillKind.Damage, "bleed",  null),
            ("stab",         "Stab",         3, SkillTarget.Enemy, SkillKind.Damage, "bleed",  null),
            ("gaze",         "Gaze",         2, SkillTarget.Enemy, SkillKind.Damage, "shaken", null),
            ("burst",        "Burst",        4, SkillTarget.Enemy, SkillKind.Damage, "burn",   "shaken"),
            ("tusk",         "Tusk",         4, SkillTarget.Enemy, SkillKind.Damage, null,     null),
            ("slam",         "Slam",         5, SkillTarget.Enemy, SkillKind.Damage, "shaken", null),
            ("scorch",       "Scorch",       5, SkillTarget.Enemy, SkillKind.Damage, "burn",   null),
        };

        private static readonly string[] ChainSkillIds = { "flame_pillar", "fissure", "rend" };

        private static readonly string[] NamePool = { "Mira", "Tobin", "Elsa", "Garrick", "Nyx", "Bram", "Lena", "Odo" };

        // Indices into Tiles-Sheet.png (sliced 32x32). Floor: plain tile first, decoration after.
        private const string TileSheet = Export + "/Tiles/Tiles-Sheet.png";

        [MenuItem("Chaining Chronicle/Build Content")]
        public static void Build()
        {
            SpritePivotNormalizer.NormalizeAll();

            EnsureFolder(ContentRoot);
            EnsureFolder(ContentRoot + "/Animators");
            EnsureFolder(ContentRoot + "/Animations");
            _spritesById = null;
            EnsureFolder(ContentRoot + "/Enemies");
            EnsureFolder(ContentRoot + "/Biomes");
            EnsureFolder(ContentRoot + "/Combat");
            EnsureFolder(ContentRoot + "/Combat/Statuses");
            EnsureFolder(ContentRoot + "/Combat/Skills");
            EnsureFolder(ContentRoot + "/Party");

            // The baseline only has an idle animation facing down. For the other facings the neutral frame of
            // the matching walk clip is used as a standing pose, so the stored facing is always visible.
            const string Hero = Export + "/MainCharacter/";
            var playerController = EnsureController(ContentRoot + "/Animators/Player.controller",
                new StateSpec("idleDown", Hero + "idle.anim"),
                new StateSpec("idleUp", Hero + "walkUp.anim", ClipKind.Still),
                new StateSpec("idleLeft", Hero + "walkLeft.anim", ClipKind.Still),
                new StateSpec("idleRight", Hero + "walkRight.anim", ClipKind.Still),
                new StateSpec("walkDown", Hero + "walkDown.anim", ClipKind.WalkCycle),
                new StateSpec("walkUp", Hero + "walkUp.anim", ClipKind.WalkCycle),
                new StateSpec("walkLeft", Hero + "walkLeft.anim", ClipKind.WalkCycle),
                new StateSpec("walkRight", Hero + "walkRight.anim", ClipKind.WalkCycle));
            var pickupController = EnsureController(ContentRoot + "/Animators/Pickup.controller",
                new StateSpec("bounce", Export + "/PowerUp/BounciingExclemationMark.anim"));

            var statuses = BuildStatuses();
            var skills = BuildSkills(statuses);

            var enemyDefs = new Dictionary<string, EnemyDef>();
            foreach (var entry in Enemies)
            {
                var controller = EnsureController($"{ContentRoot}/Animators/{entry.Id}.controller", new StateSpec("idle", $"{Export}/{entry.Clip}"));
                var def = LoadOrCreate<EnemyDef>($"{ContentRoot}/Enemies/{entry.Id}.asset");
                def.id = entry.Id;
                def.displayName = entry.DisplayName;
                def.animator = controller;
                def.firstStage = entry.FirstStage;
                def.maxHp = entry.Hp;
                def.attack = entry.Attack;
                def.defense = entry.Defense;
                def.speed = entry.Speed;
                def.hpPerLevel = 4;
                def.attackPerLevel = 1;
                def.defensePerLevel = 0;
                def.speedPerLevel = 0;
                def.baseXp = entry.BaseXp;
                def.ai = entry.Ai;
                def.skills = entry.Skills.Select(s => skills[s]).ToArray();
                EditorUtility.SetDirty(def);
                enemyDefs[entry.Id] = def;
            }

            var stairs = AssetDatabase.LoadAssetAtPath<Sprite>(Export + "/Tiles/StairsDown.png");
            var snowFloor = AssetDatabase.LoadAssetAtPath<Sprite>(Export + "/Tiles/SnowTile.png");
            if (stairs == null || snowFloor == null) throw new InvalidOperationException("Stairs or snow tile sprite missing");

            var meadow = Biome("Meadow", "meadow", Sheet(6, 2, 11, 12, 1, 7), Sheet(9), stairs,
                enemyDefs, "mouse", "hooddude", "evileye");
            var snow = Biome("Snow", "snow", new[] { snowFloor }, Sheet(10), stairs,
                enemyDefs, "mouse", "walrus", "livingarmour");
            var dungeon = Biome("Dungeon", "dungeon", Sheet(13), Sheet(5), stairs,
                enemyDefs, "evileye", "livingarmour", "magmaegg");

            var content = LoadOrCreate<GameContent>(ContentRoot + "/GameContent.asset");
            content.biomes = new[] { meadow, snow, dungeon };
            content.stagesPerBiome = 3;
            content.playerAnimator = playerController;
            content.pickupAnimator = pickupController;

            content.hero = Member("hero", "Hero", playerController, Color.white,
                maxHp: 30, attack: 5, defense: 2, speed: 10, skills: new[] { skills["slash"], skills["mend"] });
            content.recruits = new[]
            {
                Member("wanderer", "Wanderer", playerController, new Color(0.75f, 1f, 0.75f),
                    maxHp: 26, attack: 6, defense: 1, speed: 11, skills: new[] { skills["flame_pillar"] }),
                Member("mystic", "Mystic", playerController, new Color(0.75f, 0.8f, 1f),
                    maxHp: 22, attack: 4, defense: 1, speed: 9, skills: new[] { skills["fissure"], skills["mend"] }),
                Member("brawler", "Brawler", playerController, new Color(1f, 0.8f, 0.7f),
                    maxHp: 34, attack: 7, defense: 3, speed: 7, skills: new[] { skills["rend"] }),
            };
            content.chainSkills = ChainSkillIds.Select(i => skills[i]).ToArray();
            content.namePool = NamePool;
            content.chainMax = 5;
            content.sameSkillOncePerCharacter = false;
            content.fleeChance = 1f;
            content.joinRadius = 5;
            content.maxEnemiesInCombat = 4;
            EditorUtility.SetDirty(content);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Chaining Chronicle content built: " + ContentRoot);
        }

        private static Dictionary<string, StatusDef> BuildStatuses()
        {
            var result = new Dictionary<string, StatusDef>();
            foreach (var (id, name, color) in StatusTable)
            {
                var def = LoadOrCreate<StatusDef>($"{ContentRoot}/Combat/Statuses/{id}.asset");
                def.id = id;
                def.displayName = name;
                def.color = color;
                EditorUtility.SetDirty(def);
                result[id] = def;
            }
            return result;
        }

        private static Dictionary<string, SkillDef> BuildSkills(Dictionary<string, StatusDef> statuses)
        {
            var result = new Dictionary<string, SkillDef>();
            foreach (var (id, name, power, target, kind, applies, followUp) in SkillTable)
            {
                var def = LoadOrCreate<SkillDef>($"{ContentRoot}/Combat/Skills/{id}.asset");
                def.id = id;
                def.displayName = name;
                def.power = power;
                def.target = target;
                def.kind = kind;
                def.statusTurns = 3;
                def.appliesStatus = applies != null ? statuses[applies] : null;
                def.followUpOn = followUp != null ? statuses[followUp] : null;
                EditorUtility.SetDirty(def);
                result[id] = def;
            }
            return result;
        }

        private static PartyMemberDef Member(string id, string displayName, RuntimeAnimatorController animator, Color tint,
            int maxHp, int attack, int defense, int speed, SkillDef[] skills)
        {
            var def = LoadOrCreate<PartyMemberDef>($"{ContentRoot}/Party/{id}.asset");
            def.id = id;
            def.displayName = displayName;
            def.animator = animator;
            def.tint = tint;
            def.maxHp = maxHp;
            def.attack = attack;
            def.defense = defense;
            def.speed = speed;
            def.hpPerLevel = 6;
            def.attackPerLevel = 1;
            def.defensePerLevel = 1;
            def.speedPerLevel = 1;
            def.startSkills = skills;
            EditorUtility.SetDirty(def);
            return def;
        }

        private static BiomeDef Biome(string assetName, string id, Sprite[] floors, Sprite[] walls, Sprite stairs,
            Dictionary<string, EnemyDef> enemies, params string[] enemyIds)
        {
            var biome = LoadOrCreate<BiomeDef>($"{ContentRoot}/Biomes/{assetName}.asset");
            biome.id = id;
            biome.floorSprites = floors;
            biome.wallSprites = walls;
            biome.stairsSprite = stairs;
            biome.enemies = enemyIds.Select(e => enemies[e]).ToArray();
            EditorUtility.SetDirty(biome);
            return biome;
        }

        private static Sprite[] Sheet(params int[] indices)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TileSheet).OfType<Sprite>().ToDictionary(s => s.name);
            return indices.Select(i =>
            {
                string name = $"Tiles-Sheet_{i}";
                if (!sprites.TryGetValue(name, out var sprite)) throw new InvalidOperationException($"Sprite {name} not found in {TileSheet}");
                return sprite;
            }).ToArray();
        }

        /// <summary>
        /// Creates the controller, or brings an existing one in line: one state per spec, states that are
        /// no longer listed are removed, the first spec is the default state.
        /// </summary>
        private static AnimatorController EnsureController(string path, params StateSpec[] specs)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;

            bool hasWalk = specs.Any(s => s.Kind == ClipKind.WalkCycle);
            if (hasWalk && controller.parameters.All(p => p.name != WalkSpeedParameter))
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = WalkSpeedParameter,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f
                });
            }

            foreach (var spec in specs)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == spec.Name)
                            ?? machine.AddState(spec.Name);
                state.motion = RebuildClip(spec.SourcePath, spec.Kind, spec.ClipName);
                bool scaled = spec.Kind == ClipKind.WalkCycle;
                state.speedParameter = scaled ? WalkSpeedParameter : string.Empty;
                state.speedParameterActive = scaled;
            }

            foreach (var obsolete in machine.states.Where(s => specs.All(x => x.Name != s.state.name)).ToArray())
                machine.RemoveState(obsolete.state);

            machine.defaultState = machine.states.Select(s => s.state).First(s => s.name == specs[0].Name);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // The baseline clips in Assets/Export reference sprites by a texture GUID that does not match the
        // imported textures of this project (only the GUID differs, the sprite ids inside the texture are
        // the same). They would animate to "no sprite". The baseline files stay untouched: a copy with the
        // same timing is written to Assets/Content/Animations, bound to the sprites of the current import.
        private static AnimationClip RebuildClip(string sourcePath, ClipKind kind, string clipName)
        {
            string text = System.IO.File.ReadAllText(sourcePath);
            var keys = System.Text.RegularExpressions.Regex.Matches(text,
                @"- time: (?<t>[-0-9.eE]+)\s+value: \{fileID: (?<id>-?\d+), guid: [0-9a-f]+, type: 3\}");
            if (keys.Count == 0) throw new InvalidOperationException($"No sprite keyframes found in {sourcePath}");

            var sprites = SpritesByLocalId();
            var frames = new ObjectReferenceKeyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                long id = long.Parse(keys[i].Groups["id"].Value);
                if (!sprites.TryGetValue(id, out var sprite))
                    throw new InvalidOperationException($"Sprite id {id} from {sourcePath} not found in any imported texture");
                frames[i] = new ObjectReferenceKeyframe
                {
                    time = float.Parse(keys[i].Groups["t"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    value = sprite
                };
            }

            var rate = System.Text.RegularExpressions.Regex.Match(text, @"m_SampleRate: ([0-9.]+)");
            var loop = System.Text.RegularExpressions.Regex.Match(text, @"m_LoopTime: (\d)");

            frames = Retime(frames, kind);

            string clipPath = $"{ContentRoot}/Animations/{clipName}_{System.IO.Directory.GetParent(sourcePath).Name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = kind != ClipKind.Baseline ? BuiltClipFrameRate
                : rate.Success ? float.Parse(rate.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                : 12f;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = kind != ClipKind.Baseline || !loop.Success || loop.Groups[1].Value == "1";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>
        /// Walk cycle: the baseline clips have different lengths (0.6 to 1.4 s) and some end with a repeat of the
        /// first frame to close the loop. Drop that repeat and space the frames evenly over one second, so the
        /// speed of every walk state can be set the same way. Still: the first frame only, held for one second.
        /// The closing key sits one frame before the second mark because Unity adds that frame to the clip, so the
        /// clip is exactly one second long.
        /// </summary>
        private static ObjectReferenceKeyframe[] Retime(ObjectReferenceKeyframe[] source, ClipKind kind)
        {
            if (kind == ClipKind.Baseline) return source;

            var first = source[0].value;
            float span = 1f - 1f / BuiltClipFrameRate;
            if (kind == ClipKind.Still)
            {
                return new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = first },
                    new ObjectReferenceKeyframe { time = span, value = first }
                };
            }

            int count = source.Length;
            if (count > 1 && source[count - 1].value == source[0].value) count--;

            var result = new ObjectReferenceKeyframe[count + 1];
            for (int i = 0; i < count; i++)
                result[i] = new ObjectReferenceKeyframe { time = span * i / count, value = source[i].value };
            // The closing key gives the last frame its duration; it shows the first frame again as the loop restarts.
            result[count] = new ObjectReferenceKeyframe { time = span, value = first };
            return result;
        }

        private static Dictionary<long, Sprite> _spritesById;

        private static Dictionary<long, Sprite> SpritesByLocalId()
        {
            if (_spritesById != null) return _spritesById;

            var map = new Dictionary<long, Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Export }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long localId))
                        map[localId] = sprite;
            }
            return _spritesById = map;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
