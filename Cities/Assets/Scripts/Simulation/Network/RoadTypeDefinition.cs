using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Network
{
    [CreateAssetMenu(fileName = "RoadType", menuName = "Cities/Definitions/Road type")]
    public sealed class RoadTypeDefinition : GameDefinition
    {
        [SerializeField] private RoadTypeSpec _spec;

        public RoadTypeSpec Spec => _spec;

        protected override string Kind => "road";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
