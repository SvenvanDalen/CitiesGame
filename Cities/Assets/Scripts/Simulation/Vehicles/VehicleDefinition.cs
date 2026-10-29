using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Vehicles
{
    [CreateAssetMenu(fileName = "Vehicle", menuName = "Cities/Definitions/Vehicle")]
    public sealed class VehicleDefinition : GameDefinition
    {
        [SerializeField] private VehicleSpec _spec;
        [SerializeField] private GameObject _prefab;

        public VehicleSpec Spec => _spec;

        /// <summary>Model voor rendering (E08, E23-06); mag ontbreken zolang er geen model is.</summary>
        public GameObject Prefab => _prefab;

        protected override string Kind => "vehicle";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
