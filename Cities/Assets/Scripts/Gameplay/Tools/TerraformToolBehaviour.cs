using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Maakt de <see cref="TerraformTool"/> voor de kaart van de scene.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class TerraformToolBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private bool _selectOnStart;

        public TerraformTool Tool { get; private set; }

        public ToolController Controller => _tools.Controller;

        /// <summary>Na de <see cref="WorldMapHostBehaviour"/> (die de kaart maakt) en vóór de UI (die de tool nodig heeft).</summary>
        private void Awake() => Tool = new TerraformTool(_world.Map);

        private void Start()
        {
            if (_selectOnStart)
            {
                _tools.Controller.Select(Tool);
            }
        }
    }
}
