using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Trees
{
    /// <summary>Plant bij het starten bossen op de kaart, na het terrein en de grondbedekking.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ForestGeneratorBehaviour : MonoBehaviour
    {
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private ForestSettings _settings = ForestSettings.Default;

        private void Start() => ForestGenerator.Generate(_world.Map, _settings);
    }
}
