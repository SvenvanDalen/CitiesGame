using System;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Controls
{
    /// <summary>Sneltoetsen voor de simulatieklok uit de map <c>Game</c>: pauze en snelheid 1×, 2× en 3×.</summary>
    public sealed class SimulationShortcutsBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private SimulationHostBehaviour _host;

        private InputAction _pause;
        private InputAction _normal;
        private InputAction _fast;
        private InputAction _fastest;
        private Action<InputAction.CallbackContext> _onPause;
        private Action<InputAction.CallbackContext> _onNormal;
        private Action<InputAction.CallbackContext> _onFast;
        private Action<InputAction.CallbackContext> _onFastest;

        private void Awake()
        {
            var map = _controls.FindActionMap("Game", throwIfNotFound: true);
            _pause = map.FindAction("Pause", true);
            _normal = map.FindAction("SpeedNormal", true);
            _fast = map.FindAction("SpeedFast", true);
            _fastest = map.FindAction("SpeedFastest", true);
            _onPause = _ => _host.Clock.TogglePause();
            _onNormal = _ => _host.Clock.SetSpeed(SimulationSpeed.Normal);
            _onFast = _ => _host.Clock.SetSpeed(SimulationSpeed.Fast);
            _onFastest = _ => _host.Clock.SetSpeed(SimulationSpeed.Fastest);
        }

        private void OnEnable()
        {
            _pause.performed += _onPause;
            _normal.performed += _onNormal;
            _fast.performed += _onFast;
            _fastest.performed += _onFastest;
            _controls.FindActionMap("Game", true).Enable();
        }

        private void OnDisable()
        {
            _pause.performed -= _onPause;
            _normal.performed -= _onNormal;
            _fast.performed -= _onFast;
            _fastest.performed -= _onFastest;
        }
    }
}
