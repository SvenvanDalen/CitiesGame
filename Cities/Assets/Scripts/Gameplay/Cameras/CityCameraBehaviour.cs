using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Cameras
{
    /// <summary>
    /// Vrije stadscamera: leest de acties uit de map <c>Camera</c> van <c>CityControls.inputactions</c> en laat de
    /// <see cref="CameraController"/> de camera besturen. Gebruikt ongeschaalde tijd, zodat de camera ook in pauze werkt.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CityCameraBehaviour : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _controls;
        [SerializeField] private CameraSettings _settings = CameraSettings.Default;

        [Tooltip("Startpunt op de grond (X, Z) in meters; standaard het midden van de kaart.")]
        [SerializeField] private Vector2 _startPivot = new Vector2(9216f, 9216f);
        [SerializeField] private float _startYaw = 30f;
        [SerializeField] private float _startPitch = 50f;
        [SerializeField] private float _startDistance = 600f;

        [Tooltip("Factor van de muiswielwaarde naar scrollstappen (1 = één stap per klik).")]
        [SerializeField] private float _scrollScale = 1f;

        private UnityEngine.Camera _camera;
        private InputAction _move;
        private InputAction _rotate;
        private InputAction _tilt;
        private InputAction _zoom;
        private InputAction _scroll;
        private InputAction _pointer;
        private InputAction _pointerDelta;
        private InputAction _panDrag;
        private InputAction _orbitDrag;
        private InputAction _fast;

        public CameraController Controller { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            var world = WorldDimensions.Default;
            Controller = new CameraController(
                _settings,
                new CameraRig(new float3(_startPivot.x, 0f, _startPivot.y), _startYaw, _startPitch, _startDistance),
                float2.zero,
                new float2(world.SizeX, world.SizeZ));

            var map = _controls.FindActionMap("Camera", throwIfNotFound: true);
            _move = map.FindAction("Move", true);
            _rotate = map.FindAction("Rotate", true);
            _tilt = map.FindAction("Tilt", true);
            _zoom = map.FindAction("Zoom", true);
            _scroll = map.FindAction("Scroll", true);
            _pointer = map.FindAction("Pointer", true);
            _pointerDelta = map.FindAction("PointerDelta", true);
            _panDrag = map.FindAction("PanDrag", true);
            _orbitDrag = map.FindAction("OrbitDrag", true);
            _fast = map.FindAction("Fast", true);
            Apply(Controller.Current);
        }

        private void OnEnable() => _controls.FindActionMap("Camera", true).Enable();

        private void OnDisable() => _controls.FindActionMap("Camera", true).Disable();

        private void LateUpdate()
        {
            var pointerDelta = (float2)_pointerDelta.ReadValue<Vector2>();
            var input = new CameraInput
            {
                Move = _move.ReadValue<Vector2>(),
                Rotate = _rotate.ReadValue<float>(),
                Tilt = _tilt.ReadValue<float>(),
                Zoom = _zoom.ReadValue<float>(),
                ScrollSteps = _scroll.ReadValue<float>() * _scrollScale,
                DragPanPixels = _panDrag.IsPressed() ? pointerDelta : float2.zero,
                OrbitPixels = _orbitDrag.IsPressed() ? pointerDelta : float2.zero,
                Fast = _fast.IsPressed(),
                PointerPosition = _pointer.ReadValue<Vector2>(),
                ScreenSize = new float2(Screen.width, Screen.height),
            };

            Controller.Settings = _settings;
            Controller.Update(input, UnityEngine.Time.unscaledDeltaTime);
            Apply(Controller.Current);
        }

        private void Apply(CameraRig rig)
        {
            transform.SetPositionAndRotation(rig.Position, rig.Rotation);
            _camera.fieldOfView = _settings.FieldOfView;
        }
    }
}
