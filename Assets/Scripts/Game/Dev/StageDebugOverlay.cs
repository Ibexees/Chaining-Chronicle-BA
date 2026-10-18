using System.Linq;
using CC.Core.Flow;
using CC.Game.Boot;
using CC.Game.Stage;
using UnityEngine;

namespace CC.Game.Dev
{
    /// <summary>TEMPORARY stage readout until the HUD exists (UI milestone).</summary>
    public sealed class StageDebugOverlay : MonoBehaviour
    {
        [SerializeField] private StageController stage;

        private void OnGUI()
        {
            if (stage == null || stage.Session == null) return;
            if (!Services.TryGet(out GameFlow flow) || flow.Current != GameFlowState.Exploring) return;

            var party = stage.Run.Party.Members.Select(m =>
                $"{m.Name} Lv{m.Level} {m.Hp}/{m.Stats.MaxHp} HP  XP {m.Xp}/{m.XpToNextLevel}  " +
                $"[{string.Join(", ", m.Skills.Select(s => s.Name))}]");
            GUI.Label(new Rect(20, 20, 900, 160),
                $"Stage {stage.StageNumber}   Seed {stage.Seed}   Enemies {stage.Session.Enemies.Count}\n" +
                "WASD / arrows: move    E: interact (faced pickup, stairs under you)\n" +
                string.Join("\n", party) +
                (string.IsNullOrEmpty(stage.Notice) ? "" : "\n> " + stage.Notice));
        }
    }
}
