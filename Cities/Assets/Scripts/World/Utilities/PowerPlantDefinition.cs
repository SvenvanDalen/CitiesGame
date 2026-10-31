using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.World.Utilities
{
    [CreateAssetMenu(fileName = "PowerPlant", menuName = "Cities/Definitions/Power plant")]
    public sealed class PowerPlantDefinition : GameDefinition
    {
        [SerializeField] private PowerPlantSpec _spec;
        [SerializeField] private GameObject _prefab;

        public PowerPlantSpec Spec => _spec;

        /// <summary>Model voor rendering; mag ontbreken zolang er geen model is.</summary>
        public GameObject Prefab => _prefab;

        protected override string Kind => "power-plant";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
