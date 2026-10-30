using System.Collections.Generic;
using System.Diagnostics;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.World.Roads;
using UnityEngine;

namespace Cities.Presentation.Roads
{
    /// <summary>
    /// Tekent elk segment van het <see cref="RoadNetwork"/> als een mesh van wegdek, stoepen en markeringen. Houdt de
    /// segmenten bij via <see cref="RoadNetwork.SegmentChanged"/> (aanleg, sloop en laden) en bouwt per frame hoogstens
    /// <see cref="_budgetMilliseconds"/> ms aan meshes, zodat een wijziging snel zichtbaar is zonder haperingen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadRendererBehaviour : MonoBehaviour
    {
        [SerializeField] private RoadNetworkHostBehaviour _network;
        [SerializeField] private RoadMaterialSet _materials;
        [SerializeField, Min(1f)] private float _budgetMilliseconds = 8f;

        private readonly Dictionary<EntityId<SegmentKind>, Piece> _pieces = new Dictionary<EntityId<SegmentKind>, Piece>();
        private readonly List<EntityId<SegmentKind>> _pending = new List<EntityId<SegmentKind>>();
        private readonly List<LaneSpec> _specs = new List<LaneSpec>();
        private RoadNetwork _roads;

        /// <summary>Aantal segmenten met een mesh.</summary>
        public int PieceCount => _pieces.Count;

        /// <summary>Duur van de laatste bouw van één segment, in ms.</summary>
        public float LastBuildMilliseconds { get; private set; }

        private void OnEnable()
        {
            _roads = _network.Network;
            _roads.SegmentChanged += OnSegmentChanged;
            foreach (var id in _roads.Segments())
            {
                _pending.Add(id);
            }
        }

        private void OnDisable()
        {
            if (_roads != null && !_roads.IsDisposed)
            {
                _roads.SegmentChanged -= OnSegmentChanged;
            }

            foreach (var piece in _pieces.Values)
            {
                piece.Destroy();
            }

            _pieces.Clear();
            _pending.Clear();
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            var clock = Stopwatch.StartNew();
            var done = 0;
            while (done < _pending.Count && clock.Elapsed.TotalMilliseconds < _budgetMilliseconds)
            {
                Rebuild(_pending[done]);
                done++;
            }

            _pending.RemoveRange(0, done);
        }

        private void OnSegmentChanged(EntityId<SegmentKind> id)
        {
            if (!_pending.Contains(id))
            {
                _pending.Add(id);
            }
        }

        private void Rebuild(EntityId<SegmentKind> id)
        {
            if (!_roads.Contains(id))
            {
                Remove(id);
                return;
            }

            var clock = Stopwatch.StartNew();
            var segment = _roads.GetSegment(id);
            _specs.Clear();
            foreach (var lane in segment.Lanes)
            {
                _specs.Add(_roads.GetLane(lane).Spec);
            }

            if (!_pieces.TryGetValue(id, out var piece))
            {
                piece = new Piece(transform, id, _materials.Materials);
                _pieces.Add(id, piece);
            }

            RoadMeshBuilder.Build(segment.Curve, _specs).CopyTo(piece.Mesh);
            LastBuildMilliseconds = (float)clock.Elapsed.TotalMilliseconds;
        }

        private void Remove(EntityId<SegmentKind> id)
        {
            if (_pieces.TryGetValue(id, out var piece))
            {
                piece.Destroy();
                _pieces.Remove(id);
            }
        }

        /// <summary>Het object met de mesh van één segment.</summary>
        private sealed class Piece
        {
            private readonly GameObject _object;

            public Piece(Transform parent, EntityId<SegmentKind> id, Material[] materials)
            {
                Mesh = new Mesh { name = $"Weg {id.Index}" };
                _object = new GameObject(Mesh.name) { hideFlags = HideFlags.DontSave };
                _object.transform.SetParent(parent, false);
                _object.AddComponent<MeshFilter>().sharedMesh = Mesh;
                var renderer = _object.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            public Mesh Mesh { get; }

            public void Destroy()
            {
                Object.Destroy(Mesh);
                Object.Destroy(_object);
            }
        }
    }
}
