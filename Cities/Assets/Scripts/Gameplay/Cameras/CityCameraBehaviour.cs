using Cities.World.Grid;
using Cities.World.Picking;
using Cities.World.Terrain;
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

        [Tooltip("Lagen waarmee de camera botst en waarop zoomen naar de cursor richt (terrein, water, gebouwen).")]
        [SerializeField] private LayerMask _surfaceLayers = ~0;

        [Tooltip("Optioneel: volg de hoogtekaart van deze wereld in plaats van colliders.")]
        [SerializeField] private WorldMapHostBehaviour _world;

        private CameraSettings _baseSettings;
        private ITerrainHeight _terrainHeight;
        private PhysicsSurfaceHeight _surface;
        private TerrainPickLayer _heightmap;

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
        private InputAction _bookmarkSet;
        private readonly InputAction[] _bookmarkActions = new InputAction[CameraBookmarks.Slots];

        public CameraController Controller { get; private set; }

        /// <summary>Ruimere grenzen voor kantelen en afstand, voor de screenshot-modus.</summary>
        public bool FreeLook { get; set; }

        /// <summary>Bladwijzers: F5 tot en met F9 roepen terug, met Ctrl erbij slaan ze de stand op.</summary>
        public CameraBookmarks Bookmarks { get; } = new CameraBookmarks();

        /// <summary>Neemt gevoeligheid, omkeren en randscrollen uit de gebruikersinstellingen over (E27-09, E27-12).</summary>
        public void ApplyUserSettings(Cities.Core.Settings.UserSettings user) => _settings = CameraUserSettings.Apply(_baseSettings, user);

        /// <summary>Springt naar een plek op de kaart (bijvoorbeeld de plek van een melding).</summary>
        public void FocusOn(float2 location)
        {
            var height = _terrainHeight != null ? _terrainHeight.SurfaceHeight(location) : 0f;
            Controller.Teleport(CameraFocus.On(Controller.Target, location, height, FocusDistance));
        }

        private const float FocusDistance = 300f;

        private void Awake()
        {
            _baseSettings = _settings;
            _camera = GetComponent<UnityEngine.Camera>();
            var world = WorldDimensions.Default;
            _surface = new PhysicsSurfaceHeight(_surfaceLayers);
            ITerrainHeight terrain = _surface;
            if (_world != null && _world.Map != null)
            {
                world = _world.Map.Dimensions;
                terrain = new WorldMapTerrain(_world.Map);
                _heightmap = new TerrainPickLayer(terrain, new float2(world.SizeX, world.SizeZ));
            }

            _terrainHeight = terrain;
            Controller = new CameraController(
                _settings,
                new CameraRig(new float3(_startPivot.x, 0f, _startPivot.y), _startYaw, _startPitch, _startDistance),
                float2.zero,
                new float2(world.SizeX, world.SizeZ),
                terrain);

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
            _bookmarkSet = map.FindAction("BookmarkSet", true);
            for (var i = 0; i < _bookmarkActions.Length; i++)
            {
                _bookmarkActions[i] = map.FindAction("Bookmark" + (i + 1), true);
            }

            Apply(Controller.Current);
        }

        private void OnEnable() => _controls.FindActionMap("Camera", true).Enable();

        private void OnDisable() => _controls.FindActionMap("Camera", true).Disable();

        public FollowCameraController Follower { get; } = new FollowCameraController();

        /// <summary>Volgt een burger of voertuig; de vrije camera pauzeert tot <see cref="StopFollowing"/> of het doel wegvalt.</summary>
        public void Follow(IFollowTarget target, FollowMode mode) => Follower.Follow(target, mode);

        /// <summary>Stopt met volgen; de vrije camera gaat verder vanaf de huidige stand.</summary>
        public void StopFollowing()
        {
            if (Follower.IsFollowing)
            {
                EndFollow();
            }
        }

        private void EndFollow()
        {
            var rig = Follower.Current;
            Follower.Stop();

            // Vanaf straatniveau terug naar de stadscamera: het draaipunt ligt dan een stuk voor de camera.
            var distance = math.max(rig.Distance, _settings.MinDistance);
            var pitch = math.max(rig.Pitch, _settings.MinPitch);
            var pivot = rig.Position + (new CameraRig(rig.Pivot, rig.Yaw, pitch, 1f).Forward * distance);
            Controller.Teleport(new CameraRig(pivot, rig.Yaw, pitch, distance));
        }

        private void LateUpdate()
        {
            var pointerDelta = (float2)_pointerDelta.ReadValue<Vector2>();
            if (Follower.IsFollowing)
            {
                var look = _orbitDrag.IsPressed() ? new float2(pointerDelta.x, -pointerDelta.y) * _settings.MouseOrbitSensitivity : float2.zero;
                if (Follower.Update(look, UnityEngine.Time.unscaledDeltaTime))
                {
                    Apply(Follower.Current);
                    return;
                }

                EndFollow();
            }

            var pointer = _pointer.ReadValue<Vector2>();
            var scroll = _scroll.ReadValue<float>() * _scrollScale;
            var zoom = _zoom.ReadValue<float>();
            var hasCursorPoint = false;
            var cursorPoint = float3.zero;
            if (math.abs(scroll) > 0f || math.abs(zoom) > 0f)
            {
                // Alleen raycasten als er gezoomd wordt; dat scheelt een raycast per frame.
                hasCursorPoint = TryGetCursorPoint(_camera.ScreenPointToRay(pointer), out cursorPoint);
            }

            var input = new CameraInput
            {
                Move = _move.ReadValue<Vector2>(),
                Rotate = _rotate.ReadValue<float>(),
                Tilt = _tilt.ReadValue<float>(),
                Zoom = zoom,
                ScrollSteps = scroll,
                DragPanPixels = _panDrag.IsPressed() ? pointerDelta : float2.zero,
                OrbitPixels = _orbitDrag.IsPressed() ? pointerDelta : float2.zero,
                Fast = _fast.IsPressed(),
                PointerPosition = pointer,
                ScreenSize = new float2(Screen.width, Screen.height),
                HasCursorPoint = hasCursorPoint,
                CursorPoint = cursorPoint,
            };

            for (var i = 0; i < _bookmarkActions.Length; i++)
            {
                if (_bookmarkActions[i].WasPressedThisFrame())
                {
                    Bookmarks.Use(i, _bookmarkSet.IsPressed(), Controller);
                }
            }

            Controller.Settings = FreeLook ? ScreenshotMode.FreeLook(_settings) : _settings;
            Controller.Update(input, UnityEngine.Time.unscaledDeltaTime);
            Apply(Controller.Current);
        }

        /// <summary>Punt onder de muis op de hoogtekaart als die er is; anders tegen colliders of het vlak van de pivot.</summary>
        private bool TryGetCursorPoint(Ray ray, out float3 point)
        {
            if (_heightmap != null && _heightmap.Raycast(new PickRay(ray.origin, ray.direction), 50_000f, out var hit))
            {
                point = hit.Point;
                return true;
            }

            return _surface.TryGetCursorPoint(ray, Controller.Current.Pivot.y, out point);
        }

        private void Apply(CameraRig rig)
        {
            transform.SetPositionAndRotation(rig.Position, rig.Rotation);
            _camera.fieldOfView = _settings.FieldOfView;
        }
    }
}
