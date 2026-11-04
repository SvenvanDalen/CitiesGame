using System;
using System.Globalization;
using System.IO;
using Cities.Simulation.Scheduling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Recording
{
    /// <summary>
    /// Neemt alle acties van een <see cref="InputActionAsset"/> op per simulatietick (E24-08) en speelt een opname
    /// af door <see cref="ActionReplayed"/> aan te roepen op de tick van elke actie. Opnames staan in
    /// <c>persistentDataPath/Recordings</c>. F10 start en stopt een opname.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputRecorderBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _host;
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private ulong _seed = 1;

        private readonly InputRecorder _recorder = new InputRecorder();
        private readonly InputPlayer _player = new InputPlayer();

        public static string Directory => Path.Combine(Application.persistentDataPath, "Recordings");

        public bool IsRecording => _recorder.IsRecording;

        public InputPlayer Player => _player;

        /// <summary>Tijdens het afspelen: de opgenomen actie op zijn tick.</summary>
        public event Action<RecordedAction> ActionReplayed;

        public void StartRecording() => _recorder.Start(_seed, Application.version);

        /// <summary>Stopt de opname en schrijft hem naar een bestand; geeft het pad terug.</summary>
        public string StopRecording()
        {
            var recording = _recorder.Stop();
            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, "input-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
            using (var writer = new StreamWriter(path))
            {
                recording.Write(writer);
            }

            return path;
        }

        public void Play(string path)
        {
            using (var reader = new StreamReader(path))
            {
                _player.Begin(InputRecording.Read(reader));
            }
        }

        private void OnEnable()
        {
            if (_actions == null)
            {
                return;
            }

            foreach (var action in _actions)
            {
                action.started += OnAction;
                action.performed += OnAction;
                action.canceled += OnAction;
            }
        }

        private void OnDisable()
        {
            if (_actions == null)
            {
                return;
            }

            foreach (var action in _actions)
            {
                action.started -= OnAction;
                action.performed -= OnAction;
                action.canceled -= OnAction;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
            {
                if (_recorder.IsRecording)
                {
                    Debug.Log("Invoeropname opgeslagen: " + StopRecording());
                }
                else
                {
                    StartRecording();
                }
            }

            if (_player.IsPlaying && _host != null && _host.Clock != null)
            {
                _player.Advance(_host.Clock.Tick, action => ActionReplayed?.Invoke(action));
            }
        }

        private void OnAction(InputAction.CallbackContext context)
        {
            if (!_recorder.IsRecording || _host == null || _host.Clock == null)
            {
                return;
            }

            var phase = context.started ? RecordedPhase.Started : context.performed ? RecordedPhase.Performed : RecordedPhase.Canceled;
            float x = 0f, y = 0f;
            if (context.valueType == typeof(Vector2))
            {
                var value = context.ReadValue<Vector2>();
                x = value.x;
                y = value.y;
            }
            else if (context.valueType == typeof(float))
            {
                x = context.ReadValue<float>();
            }

            _recorder.Record(_host.Clock.Tick, context.action.actionMap.name + "/" + context.action.name, phase, x, y);
        }
    }
}
