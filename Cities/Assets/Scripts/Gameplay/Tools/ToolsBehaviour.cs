using Cities.Gameplay.Picking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Stuurt de <see cref="ToolController"/> met de muis en het toetsenbord: <c>Tools/Apply</c> plaatst,
    /// <c>Tools/Rotate</c> draait (een penseel werkt door zolang <c>Tools/Apply</c> ingedrukt is) en <c>Game/Menu</c> (Escape) annuleert. Draait na de <see cref="PickingBehaviour"/>,
    /// zodat de preview de muis van dit frame volgt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class ToolsBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private PickingBehaviour _picking;

        private InputAction _apply;
        private InputAction _rotate;
        private InputAction _cancel;

        public ToolController Controller { get; } = new ToolController();

        public PickingBehaviour Picking => _picking;

        private void Awake()
        {
            var tools = _controls.FindActionMap("Tools", throwIfNotFound: true);
            _apply = tools.FindAction("Apply", true);
            _rotate = tools.FindAction("Rotate", true);
            _cancel = _controls.FindActionMap("Game", throwIfNotFound: true).FindAction("Menu", true);
        }

        private void OnEnable()
        {
            _apply.actionMap.Enable();
            _cancel.actionMap.Enable();
        }

        private void OnDisable() => Controller.Deselect();

        /// <summary>Ctrl+Z maakt ongedaan, Ctrl+Y of Ctrl+Shift+Z doet opnieuw (E27-04).</summary>
        private void HandleUndoKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.ctrlKey.isPressed)
            {
                return;
            }

            if (keyboard.zKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                {
                    Controller.History.Redo();
                }
                else
                {
                    Controller.History.Undo();
                }
            }
            else if (keyboard.yKey.wasPressedThisFrame)
            {
                Controller.History.Redo();
            }
        }

        /// <summary>Volgt de rechtermuisknop: een korte klik zonder slepen annuleert, slepen blijft voor de camera.</summary>
        private bool RightClicked()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightDown = mouse.position.ReadValue();
                _rightDownTime = Time.unscaledTime;
            }

            return mouse.rightButton.wasReleasedThisFrame && ClickDetector.IsClick(_rightDown, _rightDownTime, mouse.position.ReadValue(), Time.unscaledTime);
        }

        private Vector2 _rightDown;
        private float _rightDownTime;

        private void Update()
        {
            HandleUndoKeys();
            if (Controller.Active == null)
            {
                return;
            }

            if (RightClicked())
            {
                Controller.Cancel();
                return;
            }

            // Pollen in Update: het menu luistert naar 'performed', dat eerder in het frame komt en dan nog ziet dat er een tool actief is.
            if (_cancel.WasPressedThisFrame())
            {
                Controller.Cancel();
                return;
            }

            Controller.Update(new ToolInput(_picking.Current, _rotate.WasPressedThisFrame() ? 1 : 0, Time.deltaTime));
            if (_apply.WasPressedThisFrame() || (Controller.Active.RepeatsWhileHeld && _apply.IsPressed()))
            {
                Controller.Apply();
            }
        }
    }
}
