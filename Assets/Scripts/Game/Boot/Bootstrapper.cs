using CC.Core.Util;
using CC.Game.Input;
using UnityEngine;

namespace CC.Game.Boot
{
    /// <summary>
    /// Entry point, lives in the Boot scene. Registers the persistent services and starts the game flow.
    /// The game must always be started from the Boot scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(GameFlow), typeof(InputRouter))]
    public sealed class Bootstrapper : MonoBehaviour
    {
        // Static state survives when "Enter Play Mode without domain reload" is enabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Services.Clear();
            GameEvents.ClearSubscribers();
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);

            var flow = GetComponent<GameFlow>();
            var input = GetComponent<InputRouter>();
            Services.Register(flow);
            Services.Register(input);

            flow.Begin();
        }
    }
}
