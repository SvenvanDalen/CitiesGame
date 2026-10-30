using Cities.World.Picking;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Picking
{
    /// <summary>
    /// Maakt een los geplaatst scene-object aanwijsbaar, zoals een handmatig neergezet monument of een object in een
    /// testscene. De vorm volgt de renderer: een doos voor gebouwen, een staande capsule voor agents en een capsule
    /// langs de lengte voor wegen. Simulatiesystemen gebruiken de lagen rechtstreeks in plaats van dit component.
    /// </summary>
    public sealed class PickableObjectBehaviour : MonoBehaviour
    {
        [SerializeField] private PickingBehaviour _picking;
        [SerializeField] private PickKind _kind = PickKind.Building;

        [Tooltip("Index van het object; met generatie 1 is dat de ID die tools terugkrijgen.")]
        [SerializeField] private int _index;

        private Renderer _renderer;
        private bool _registered;

        /// <summary>De ID zoals in <see cref="PickHit.Id"/>.</summary>
        public ulong Id => (1UL << 32) | (uint)_index;

        public PickKind Kind => _kind;

        private void Awake() => _renderer = GetComponentInChildren<Renderer>();

        private void OnEnable() => Register();

        private void OnDisable()
        {
            if (!_registered || _picking == null || _picking.Service == null)
            {
                return;
            }

            switch (_kind)
            {
                case PickKind.Vehicle:
                case PickKind.Citizen:
                    _picking.AgentLayer(_kind).Remove(Id);
                    break;
                case PickKind.Segment:
                case PickKind.Node:
                    _picking.CapsuleLayer(_kind).Remove(Id);
                    break;
                default:
                    _picking.BoxLayer(_kind).Remove(Id);
                    break;
            }

            _registered = false;
        }

        private void LateUpdate()
        {
            // Ook als de PickingBehaviour bij OnEnable nog niet klaar was.
            if (!_registered || transform.hasChanged)
            {
                transform.hasChanged = false;
                Register();
            }
        }

        private void Register()
        {
            if (_picking == null || _picking.Service == null || _renderer == null || _kind == PickKind.None || _kind == PickKind.Terrain)
            {
                return;
            }

            var local = _renderer.localBounds;
            var shape = _renderer.transform;
            float3 center = shape.TransformPoint(local.center);
            float3 halfExtents = Vector3.Scale(local.extents, shape.lossyScale);
            halfExtents = math.abs(halfExtents);
            var yaw = math.radians(shape.eulerAngles.y);
            switch (_kind)
            {
                case PickKind.Vehicle:
                case PickKind.Citizen:
                    var feet = center - new float3(0f, halfExtents.y, 0f);
                    _picking.AgentLayer(_kind).Set(new PickAgent(Id, feet, math.max(halfExtents.x, halfExtents.z), halfExtents.y * 2f));
                    break;
                case PickKind.Segment:
                case PickKind.Node:
                    _picking.CapsuleLayer(_kind).Set(Capsule(center, halfExtents, yaw));
                    break;
                default:
                    _picking.BoxLayer(_kind).Set(new PickBox(Id, center, halfExtents, yaw));
                    break;
            }

            _registered = true;
        }

        /// <summary>Capsule langs de langste horizontale as, op de bovenkant van het object (het wegdek).</summary>
        private PickCapsule Capsule(float3 center, float3 halfExtents, float yaw)
        {
            var alongX = halfExtents.x >= halfExtents.z;
            var half = alongX ? halfExtents.x : halfExtents.z;
            var radius = alongX ? halfExtents.z : halfExtents.x;
            math.sincos(yaw, out var sin, out var cos);

            // Lokaal +X wordt (cos, -sin), lokaal +Z wordt (sin, cos) in wereld X/Z bij Unity's draaiing om Y.
            var axis = alongX ? new float3(cos, 0f, -sin) : new float3(sin, 0f, cos);
            var top = center + new float3(0f, halfExtents.y, 0f);
            var reach = math.max(half - radius, 0f);
            return new PickCapsule(Id, top - (axis * reach), top + (axis * reach), radius);
        }
    }
}
