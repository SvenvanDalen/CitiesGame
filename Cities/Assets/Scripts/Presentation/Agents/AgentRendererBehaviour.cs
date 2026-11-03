using System.Collections.Generic;
using Cities.Simulation.Animation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Agents
{
    /// <summary>
    /// Tekent één soort geanimeerde agent (een voertuigtype of burgers) met één drawcall (E21-05). De aanroeper levert per frame de
    /// zichtbare agents met <see cref="Submit"/> (bijvoorbeeld de uitkomst van <c>VehicleLod</c> of <c>AgentLod</c>); de animatiestand
    /// volgt uit hun verplaatsing en gaat samen met de plek in twee structured buffers naar de shader <c>Cities/CityAgent</c>. Er is geen
    /// skinning en geen CPU-werk per frame voor de animatie zelf; geheugen en allocaties blijven constant zodra de capaciteit bereikt is.
    /// </summary>
    public sealed class AgentRendererBehaviour : MonoBehaviour
    {
        private static readonly int PoseId = Shader.PropertyToID("_AgentPose");
        private static readonly int AnimId = Shader.PropertyToID("_AgentAnim");

        [SerializeField] private Mesh _mesh;
        [SerializeField, Tooltip("Eén materiaal per submesh, allemaal met de shader Cities/CityAgent.")] private Material[] _materials;
        [SerializeField, Tooltip("Wiel voor voertuigen, Walk voor burgers met een gebakken clip.")] private AgentAnimationKind _kind;
        [SerializeField, Tooltip("Wielstraal in meters (voertuigen) of schaal van de burger (1 = volwassene).")] private float _size = 0.3f;
        [SerializeField] private int _initialCapacity = 256;
        [SerializeField] private float _boundsRadius = 4000f;

        private readonly List<float4> _pose = new List<float4>();
        private readonly List<float4> _anim = new List<float4>();
        private AgentAnimationTracker _tracker;
        private GraphicsBuffer _poseBuffer;
        private GraphicsBuffer _animBuffer;
        private MaterialPropertyBlock _properties;
        private int _capacity;

        /// <summary>Aantal agents dat het laatst getekend is.</summary>
        public int DrawnCount { get; private set; }

        public void Configure(Mesh mesh, Material[] materials, AgentAnimationKind kind, float size)
        {
            _mesh = mesh;
            _materials = materials;
            _kind = kind;
            _size = size;
            _tracker = null;
        }

        /// <summary>Begin een frame; wist de eerder aangeleverde agents.</summary>
        public void BeginFrame()
        {
            EnsureTracker();
            _tracker.Begin();
            _pose.Clear();
            _anim.Clear();
        }

        /// <summary>Meldt een zichtbare agent: stabiel id, plek in de wereld, kijkrichting (radialen om Y) en schaal.</summary>
        public void Submit(uint id, float3 position, float yaw, float scale = 1f)
        {
            EnsureTracker();
            var forward = new float2(math.sin(yaw), math.cos(yaw));
            var value = _tracker.Update(id, position.xz, forward);
            _pose.Add(new float4(position, yaw));
            _anim.Add(new float4(value, scale, 0f, 0f));
        }

        /// <summary>Sluit het frame: vergeet verdwenen agents en tekent de rest.</summary>
        public void EndFrame()
        {
            _tracker.End();
            DrawnCount = _pose.Count;
            if (DrawnCount == 0 || _mesh == null || _materials == null)
            {
                return;
            }

            Upload();
            var bounds = new Bounds(transform.position, Vector3.one * (_boundsRadius * 2f));
            var submeshes = math.min(_materials.Length, _mesh.subMeshCount);
            for (var i = 0; i < submeshes; i++)
            {
                if (_materials[i] == null)
                {
                    continue;
                }

                var parameters = new RenderParams(_materials[i])
                {
                    worldBounds = bounds,
                    matProps = _properties,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                };
                Graphics.RenderMeshPrimitives(parameters, _mesh, i, DrawnCount);
            }
        }

        private void EnsureTracker() => _tracker ??= new AgentAnimationTracker(_kind, math.max(0.01f, _size));

        private void Upload()
        {
            if (_poseBuffer == null || DrawnCount > _capacity)
            {
                ReleaseBuffers();
                _capacity = math.max(_initialCapacity, math.ceilpow2(DrawnCount));
                _poseBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _capacity, sizeof(float) * 4);
                _animBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _capacity, sizeof(float) * 4);
                _properties ??= new MaterialPropertyBlock();
                _properties.SetBuffer(PoseId, _poseBuffer);
                _properties.SetBuffer(AnimId, _animBuffer);
            }

            _poseBuffer.SetData(_pose, 0, 0, DrawnCount);
            _animBuffer.SetData(_anim, 0, 0, DrawnCount);
        }

        private void ReleaseBuffers()
        {
            _poseBuffer?.Release();
            _animBuffer?.Release();
            _poseBuffer = null;
            _animBuffer = null;
        }

        private void OnDestroy() => ReleaseBuffers();
    }
}
