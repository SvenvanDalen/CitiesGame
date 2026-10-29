using Cities.Gameplay.Controls;
using Cities.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Presentation.Settings
{
    /// <summary>
    /// Toont het besturingsscherm in een <see cref="UIDocument"/>. De actie <c>Game/Menu</c> (standaard Escape of F10)
    /// opent en sluit het; tot het pauzemenu er is (E18-05) is dit de plek waar de speler zijn toetsen instelt.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ControlsSettingsBehaviour : MonoBehaviour
    {
        [SerializeField] private ControlBindingsBehaviour _bindings;
        [SerializeField] private bool _openOnStart;

        private UIDocument _document;
        private VisualElement _overlay;
        private InputAction _menu;

        public ControlsSettingsPanel Panel { get; private set; }

        public bool IsOpen => _overlay != null && _overlay.style.display == DisplayStyle.Flex;

        public void Open() => SetOpen(true);

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
        }

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
            _overlay.Add(Panel);
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

        private void OnMenu(InputAction.CallbackContext context) => Toggle();

        private void SetOpen(bool open)
        {
            if (_overlay != null)
            {
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
