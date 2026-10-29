using System.IO;
using Cities.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Controls
{
    /// <summary>
    /// Laadt de opgeslagen toetsen van de speler op <c>CityControls</c> voordat andere componenten acties lezen, en zet
    /// alle actiemaps aan. De instellingen staan in <c>Application.persistentDataPath/Settings/controls.json</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    public sealed class ControlBindingsBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;

        public InputActionAsset Controls => _controls;

        public InputControlBindings Bindings { get; private set; }

        public static string SettingsDirectory => Path.Combine(Application.persistentDataPath, "Settings");

        private void Awake()
        {
            Bindings = new InputControlBindings(_controls, new FileSettingsStore(SettingsDirectory));
            Bindings.Load();
        }

        private void OnEnable() => _controls.Enable();

        private void OnDisable() => _controls.Disable();

        private void OnDestroy()
        {
            Bindings?.Dispose();
            _controls.RemoveAllBindingOverrides();
        }
    }
}
