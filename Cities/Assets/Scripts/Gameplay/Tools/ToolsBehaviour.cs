using Cities.Gameplay.Picking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Stuurt de <see cref="ToolController"/> met de muis en het toetsenbord: <c>Tools/Apply</c> plaatst,
    /// <c>Tools/Rotate</c> draait en <c>Game/Menu</c> (Escape) annuleert. Draait na de <see cref="PickingBehaviour"/>,
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

        private void Update()
        {
            if (Controller.Active == null)
            {
                return;
            }

            // Pollen in Update: het menu luistert naar 'performed', dat eerder in het frame komt en dan nog ziet dat er een tool actief is.
            if (_cancel.WasPressedThisFrame())
            {
                Controller.Cancel();
                return;
            }

            Controller.Update(new ToolInput(_picking.Current, _rotate.WasPressedThisFrame() ? 1 : 0));
            if (_apply.WasPressedThisFrame())
            {
                Controller.Apply();
            }
        }
    }
}
