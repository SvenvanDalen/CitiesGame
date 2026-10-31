using Cities.World.Grid;
using Cities.World.Water;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Maakt de penselen (<see cref="TerraformTool"/> en <see cref="TreeTool"/>) voor de kaart van de scene.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class BrushToolsBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private WorldMapHostBehaviour _world;

        [Tooltip("Optioneel: de watersimulatie, voor de bron- en afvoertool.")]
        [SerializeField] private WaterHostBehaviour _water;
        [SerializeField] private bool _selectOnStart;

        public TerraformTool Terraform { get; private set; }

        public TreeTool Trees { get; private set; }

        /// <summary><c>null</c> zonder <see cref="WaterHostBehaviour"/> in de scene.</summary>
        public WaterSourceTool WaterSources { get; private set; }

        public ToolController Controller => _tools.Controller;

        /// <summary>Na de <see cref="WorldMapHostBehaviour"/> (die de kaart maakt) en vóór de UI (die de tools nodig heeft).</summary>
        private void Awake()
        {
            Terraform = new TerraformTool(_world.Map);
            Trees = new TreeTool(_world.Map);
            if (_water != null)
            {
                WaterSources = new WaterSourceTool(_water.Water.Sources, _world.Map.Dimensions);
            }
        }

        private void Start()
        {
            if (_selectOnStart)
            {
                _tools.Controller.Select(Terraform);
            }
        }
    }
}
