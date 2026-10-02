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
            GUI.Label(new Rect(20, 20, 700, 60),
                $"Stage {stage.StageNumber}   Seed {stage.Seed}   Enemies {stage.Session.Enemies.Count}\n" +
                "WASD / arrows: move    E: interact (faced pickup, stairs under you)");
        }
    }
}
