using System;
using CC.Core.Flow;
using CC.Game.Boot;
using CC.Game.Input;
using UnityEngine;

namespace CC.Game.Hub
{
    /// <summary>
    /// Entry point of the Hub scene: starts a new run. Minimal for now (confirm key + IMGUI hint);
    /// leaderboard, name input and menu come with the UI milestone.
    /// </summary>
    public sealed class HubController : MonoBehaviour
    {
        private GameFlow _flow;
        private InputRouter _input;

        private void OnEnable()
        {
            _flow = Services.Get<GameFlow>();
            _input = Services.Get<InputRouter>();
            _input.ConfirmPressed += StartRun;
        }

        private void OnDisable()
        {
            if (_input != null) _input.ConfirmPressed -= StartRun;
        }

        private void StartRun()
        {
            if (_flow.Current != GameFlowState.Hub) return;
            _flow.StartRun(Environment.TickCount);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(20, 20, 600, 30), "Chaining Chronicle  -  Enter / Space / E: start run");
        }
    }
}
