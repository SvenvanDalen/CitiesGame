using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.World.Tourism
{
    [CreateAssetMenu(fileName = "TouristSite", menuName = "Cities/Definitions/Tourist site")]
    public sealed class TouristSiteDefinition : GameDefinition
    {
        [SerializeField] private TouristSiteSpec _spec;

        public TouristSiteSpec Spec => _spec;

        protected override string Kind => "tourist-site";

        protected override void ValidateSpec(ICollection<string> errors) => _spec.Validate(errors);
    }
}
