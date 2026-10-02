using CC.Core.Flow;
using CC.Core.Util;
using CC.Game.Boot;
using CC.Game.Input;
using CC.Game.Stage;
using UnityEngine;

namespace CC.Game.Dev
{
    /// <summary>
    /// TEMPORARY placeholder for the combat system (milestone Combat), so the stage flow
    /// Exploring -> Combat -> Exploring can be played and tested end to end.
    /// Confirm counts as a win, Cancel as a flight. Both remove the enemy from the stage. Delete with the real combat.
    /// </summary>
    public sealed class TempCombatStub : MonoBehaviour
    {
        [SerializeField] private StageController stage;

        private GameFlow _flow;
        private InputRouter _input;
        private int _enemyId = -1;
        private string _label = "";

        private void OnEnable()
        {
            _flow = Services.Get<GameFlow>();
            _input = Services.Get<InputRouter>();
            GameEvents.EnemyEngaged += OnEngaged;
            _input.ConfirmPressed += OnWin;
            _input.CancelPressed += OnFlee;
        }

        private void OnDisable()
        {
            GameEvents.EnemyEngaged -= OnEngaged;
            if (_input == null) return;
            _input.ConfirmPressed -= OnWin;
            _input.CancelPressed -= OnFlee;
        }

        private void OnEngaged(int enemyId)
        {
            _enemyId = enemyId;
            var enemyView = stage.EnemyViewFor(enemyId);
            _label = enemyView != null ? enemyView.TypeId : "enemy";
        }

        private void OnWin() => Resolve("won");

        private void OnFlee() => Resolve("fled");

        private void Resolve(string outcome)
        {
            if (_flow.Current != GameFlowState.Combat || _enemyId < 0) return;

            Debug.Log($"[TEMP combat] {_label} {outcome}");
            stage.ResolveEngagement(_enemyId);
            _enemyId = -1;
            _flow.GoTo(GameFlowState.Exploring);
        }

        private void OnGUI()
        {
            if (_flow == null || _flow.Current != GameFlowState.Combat) return;
            GUI.Box(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 40, 400, 80),
                $"COMBAT vs {_label}  (placeholder)\nEnter: win    Esc: flee");
        }
    }
}
