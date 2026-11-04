using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.World.Buildings
{
    [CreateAssetMenu(fileName = "Building", menuName = "Cities/Definitions/Building")]
    public sealed class BuildingDefinition : GameDefinition, IPrefabDefinition
    {
        [SerializeField] private BuildingSpec _spec;
        [SerializeField] private GameObject _prefab;

        public BuildingSpec Spec => _spec;

        /// <summary>Model voor rendering; mag ontbreken zolang er geen model is.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Maakt een definitie tijdens het spelen, voor eigen gebouwen die uit de data-map geladen zijn (E21-08).</summary>
        public static BuildingDefinition CreateRuntime(string id, string displayName, BuildingSpec spec, GameObject prefab)
        {
            var definition = CreateInstance<BuildingDefinition>();
            definition.Initialize(id, displayName, string.Empty);
            definition._spec = spec;
            definition._prefab = prefab;
            return definition;
        }

        protected override string Kind => "building";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
