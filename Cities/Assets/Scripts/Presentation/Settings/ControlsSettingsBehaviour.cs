using Cities.Core.Settings;
using Cities.Gameplay.Cameras;
using Cities.Gameplay.Controls;
using Cities.Gameplay.Tools;
using Cities.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Presentation.Settings
{
    /// <summary>
    /// Toont het besturingsscherm in een <see cref="UIDocument"/>. De actie <c>Game/Menu</c> (standaard Escape of F10)
    /// opent en sluit het, behalve als er een tool actief is: dan annuleert de toets eerst die tool. Tot het pauzemenu
    /// er is (E18-05) is dit de plek waar de speler zijn toetsen instelt.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ControlsSettingsBehaviour : MonoBehaviour
    {
        [SerializeField] private ControlBindingsBehaviour _bindings;
        [SerializeField] private bool _openOnStart;

        [Tooltip("Optioneel: Escape annuleert eerst de actieve tool voordat het menu opent.")]
        [SerializeField] private ToolsBehaviour _tools;

        private UIDocument _document;
        private UserSettings _user;
        private FileSettingsStore _store;
        private OptionsPanel _options;
        private VisualElement _tabs;
        private VisualElement _overlay;
        private InputAction _menu;

        public ControlsSettingsPanel Panel { get; private set; }

        public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

        public void Open()
        {
            // Een open menu en een actieve tool zouden allebei op dezelfde Escape reageren.
            if (_tools != null)
            {
                _tools.Controller.Deselect();
            }

            SetOpen(true);
        }

        public void Close()
        {
            Panel?.Bindings.CancelRebind();
            SetOpen(false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _menu = _bindings.Controls.FindActionMap("Game", throwIfNotFound: true).FindAction("Menu", true);
            _store = new FileSettingsStore(ControlBindingsBehaviour.SettingsDirectory);
            _user = new UserSettings();
            _user.Load(_store);
        }

        private void Start() => ApplyUser();

        private void ApplyUser() => OptionsApplier.Apply(_user, _document.panelSettings, FindAnyObjectByType<CityCameraBehaviour>());

        private void OnEnable()
        {
            var root = _document.rootVisualElement;
            root.AddToClassList(UiClasses.Root);

            _overlay = new VisualElement { name = "controls-overlay", pickingMode = PickingMode.Position };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.top = 0;
            _overlay.style.right = 0;
            _overlay.style.bottom = 0;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.AddToClassList(UiClasses.Overlay);

            Panel = new ControlsSettingsPanel(_bindings.Bindings);
            Panel.CloseRequested += Close;
            _options = new OptionsPanel(_user);
            _options.Applied += () =>
            {
                _user.Save(_store);
                ApplyUser();
            };
            _options.CloseRequested += Close;
            _tabs = new VisualElement { name = "settings-tabs" };
            _tabs.style.flexDirection = FlexDirection.Row;
            var optionsTab = new CityButton("Opties");
            var controlsTab = new CityButton("Toetsen");
            optionsTab.clicked += () => ShowTab(true);
            controlsTab.clicked += () => ShowTab(false);
            _tabs.Add(optionsTab);
            _tabs.Add(controlsTab);
            _overlay.Add(_tabs);
            _overlay.Add(_options);
            _overlay.Add(Panel);
            ShowTab(true);
            root.Add(_overlay);
            SetOpen(_openOnStart);

            _menu.performed += OnMenu;
        }

        private void OnDisable()
        {
            _menu.performed -= OnMenu;
            if (Panel != null)
            {
                Panel.CloseRequested -= Close;
                Panel.Dispose();
            }

            _overlay?.RemoveFromHierarchy();
            _overlay = null;
            Panel = null;
        }

        private void ShowTab(bool options)
        {
            _options.style.display = options ? DisplayStyle.Flex : DisplayStyle.None;
            Panel.style.display = options ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnMenu(InputAction.CallbackContext context)
        {
            // De ToolsBehaviour annuleert de tool later in dit frame.
            if (!IsOpen && _tools != null && _tools.Controller.Active != null)
            {
                return;
            }

            Toggle();
        }

        private void SetOpen(bool open)
        {
            if (_overlay != null)
            {
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
