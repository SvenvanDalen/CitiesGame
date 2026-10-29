using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.World.Buildings
{
    [CreateAssetMenu(fileName = "Building", menuName = "Cities/Definitions/Building")]
    public sealed class BuildingDefinition : GameDefinition
    {
        [SerializeField] private BuildingSpec _spec;
        [SerializeField] private GameObject _prefab;

        public BuildingSpec Spec => _spec;

        /// <summary>Model voor rendering; mag ontbreken zolang er geen model is.</summary>
        public GameObject Prefab => _prefab;

        protected override string Kind => "building";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
