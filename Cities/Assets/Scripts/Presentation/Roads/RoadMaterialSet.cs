using System;
using Cities.World.Roads;
using UnityEngine;

namespace Cities.Presentation.Roads
{
    /// <summary>De materialen van het wegdek, één per <see cref="RoadSurface"/> in de volgorde van de enum.</summary>
    [CreateAssetMenu(menuName = "Cities/Wegmaterialen", fileName = "Roads")]
    public sealed class RoadMaterialSet : ScriptableObject
    {
        [SerializeField] private Material[] _materials = new Material[Enum.GetValues(typeof(RoadSurface)).Length];

        public Material[] Materials => _materials;

        public void Set(Material[] materials) => _materials = materials ?? throw new ArgumentNullException(nameof(materials));
    }
}
