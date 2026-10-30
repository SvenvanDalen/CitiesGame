using System.Collections.Generic;
using System.Diagnostics;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.World.Roads;
using UnityEngine;

namespace Cities.Presentation.Roads
{
    /// <summary>
    /// Tekent elk segment van het <see cref="RoadNetwork"/> als een mesh van wegdek, stoepen en markeringen, en elk
    /// kruispunt (knooppunt met drie of meer armen) als eigen mesh; de wegen erlangs houden eerder op. Houdt de
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
        private readonly Dictionary<EntityId<NodeKind>, Piece> _junctions = new Dictionary<EntityId<NodeKind>, Piece>();
        private readonly List<EntityId<NodeKind>> _pendingNodes = new List<EntityId<NodeKind>>();
        private readonly List<EntityId<SegmentKind>> _pending = new List<EntityId<SegmentKind>>();
        private readonly List<LaneSpec> _specs = new List<LaneSpec>();
        private RoadNetwork _roads;

        /// <summary>Aantal segmenten met een mesh.</summary>
        public int PieceCount => _pieces.Count;

        /// <summary>Aantal kruispunten met een mesh.</summary>
        public int JunctionCount => _junctions.Count;

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

            foreach (var id in _roads.Nodes())
            {
                _pendingNodes.Add(id);
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

            foreach (var piece in _junctions.Values)
            {
                piece.Destroy();
            }

            _pieces.Clear();
            _junctions.Clear();
            _pending.Clear();
            _pendingNodes.Clear();
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0 && _pendingNodes.Count == 0)
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

            done = 0;
            while (done < _pendingNodes.Count && clock.Elapsed.TotalMilliseconds < _budgetMilliseconds)
            {
                RebuildJunction(_pendingNodes[done]);
                done++;
            }

            _pendingNodes.RemoveRange(0, done);
        }

        private void OnSegmentChanged(EntityId<SegmentKind> id)
        {
            if (_roads.Contains(id))
            {
                var segment = _roads.GetSegment(id);
                Dirty(id);
                MarkNode(segment.Start);
                MarkNode(segment.End);
            }
            else if (_pieces.TryGetValue(id, out var piece))
            {
                Dirty(id);
                MarkNode(piece.Start);
                MarkNode(piece.End);
            }
        }

        private void Dirty(EntityId<SegmentKind> id)
        {
            if (!_pending.Contains(id))
            {
                _pending.Add(id);
            }
        }

        /// <summary>Een kruispunt verandert de afsnijding van elke weg erlangs; die wegen en het kruispunt worden opnieuw gebouwd.</summary>
        private void MarkNode(EntityId<NodeKind> node)
        {
            if (!_pendingNodes.Contains(node))
            {
                _pendingNodes.Add(node);
            }

            if (!_roads.Contains(node))
            {
                return;
            }

            foreach (var neighbour in _roads.GetNode(node).Segments)
            {
                Dirty(neighbour);
            }
        }

        private void RebuildJunction(EntityId<NodeKind> node)
        {
            var data = _roads.Contains(node) ? RoadJunctions.Build(_roads, node) : null;
            if (data == null)
            {
                if (_junctions.TryGetValue(node, out var old))
                {
                    old.Destroy();
                    _junctions.Remove(node);
                }

                return;
            }

            if (!_junctions.TryGetValue(node, out var piece))
            {
                piece = new Piece(transform, $"Kruispunt {node.Index}", _materials.Materials);
                _junctions.Add(node, piece);
            }

            data.CopyTo(piece.Mesh);
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
                piece = new Piece(transform, $"Weg {id.Index}", _materials.Materials);
                _pieces.Add(id, piece);
            }

            piece.Start = segment.Start;
            piece.End = segment.End;
            RoadMeshBuilder.Build(RoadJunctions.Trimmed(_roads, id), _specs).CopyTo(piece.Mesh);
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

            public Piece(Transform parent, string name, Material[] materials)
            {
                Mesh = new Mesh { name = name };
                _object = new GameObject(Mesh.name) { hideFlags = HideFlags.DontSave };
                _object.transform.SetParent(parent, false);
                _object.AddComponent<MeshFilter>().sharedMesh = Mesh;
                var renderer = _object.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            public Mesh Mesh { get; }

            /// <summary>De knooppunten van een wegstuk, om bij sloop de buren te kunnen bijwerken.</summary>
            public EntityId<NodeKind> Start { get; set; }

            public EntityId<NodeKind> End { get; set; }

            public void Destroy()
            {
                Object.Destroy(Mesh);
                Object.Destroy(_object);
            }
        }
    }
}
