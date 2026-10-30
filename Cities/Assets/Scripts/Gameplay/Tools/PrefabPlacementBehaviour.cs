using Cities.World.Grid;
using Cities.World.Trees;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Een <see cref="PlacementTool"/> die een prefab neerzet, tot de echte gebouwen er zijn (E05-07). Het model
    /// staat met de onderkant op de oorsprong en is <see cref="_width"/> × <see cref="_depth"/> cellen groot. Met een
    /// kaart erbij wordt de grond onder het gebouw bebouwd en verdwijnen de bomen daar.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class PrefabPlacementBehaviour : MonoBehaviour, IPlacementTarget
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private string _toolName = "Plaatsen";
        [SerializeField, Min(1)] private int _width = 2;
        [SerializeField, Min(1)] private int _depth = 2;
        [SerializeField, Min(0f)] private float _maxRise = 1.5f;
        [SerializeField] private bool _selectOnStart;

        [Tooltip("Optioneel: de kaart waarop de grond bebouwd wordt en de bomen gekapt.")]
        [SerializeField] private WorldMapHostBehaviour _world;

        private readonly FootprintOccupancy _occupancy = new FootprintOccupancy();

        public PlacementTool Tool { get; private set; }

        public FootprintOccupancy Occupancy => _occupancy;

        public string Check(in Footprint footprint) => _occupancy.Check(footprint);

        public void Place(in Footprint footprint, float3 position)
        {
            _occupancy.Add(footprint);
            if (_world != null)
            {
                TreeBrush.ClearForBuilding(_world.Map, new RectInt(footprint.Origin.X, footprint.Origin.Z, footprint.SizeX, footprint.SizeZ));
            }

            Instantiate(_prefab, position, Quaternion.Euler(0f, footprint.YawDegrees, 0f), transform);
        }

        /// <summary>Na de <see cref="Picking.PickingBehaviour"/> (die het terrein kiest) en vóór de UI (die de tool nodig heeft).</summary>
        private void Awake()
        {
            var picking = _tools.Picking;
            Tool = new PlacementTool(_toolName, _width, _depth, picking.Dimensions, picking.Terrain, this, _maxRise) { Ghost = _prefab };
        }

        private void Start()
        {
            if (_selectOnStart)
            {
                _tools.Controller.Select(Tool);
            }
        }
    }
}
