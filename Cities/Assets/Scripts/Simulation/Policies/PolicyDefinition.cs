using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Policies
{
    [CreateAssetMenu(fileName = "Policy", menuName = "Cities/Definitions/Policy")]
    public sealed class PolicyDefinition : GameDefinition
    {
        [SerializeField] private PolicySpec _spec;

        public PolicySpec Spec => _spec;

        protected override string Kind => "policy";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
