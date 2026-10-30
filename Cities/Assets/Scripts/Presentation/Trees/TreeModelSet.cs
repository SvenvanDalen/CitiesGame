using System;
using UnityEngine;

namespace Cities.Presentation.Trees
{
    /// <summary>
    /// Modellen per boomsoort (in de volgorde van <see cref="World.Trees.TreeSpecies"/>), elk in drie detailniveaus:
    /// het volle model dichtbij, een eenvoudige kroon op middenafstand en een impostor (drie vlakken met een foto van
    /// het model) in de verte. Gebouwd door <c>Cities > Modellen > Bomen</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Cities/Bomen", fileName = "Trees")]
    public sealed class TreeModelSet : ScriptableObject
    {
        [SerializeField] private TreeModel[] _species = Array.Empty<TreeModel>();

        public TreeModel[] Species
        {
            get => _species;
            set => _species = value;
        }
    }

    /// <summary>Eén boomsoort. Detail- en kroonmesh hebben submesh 0 voor de bast en 1 voor het blad.</summary>
    [Serializable]
    public sealed class TreeModel
    {
        [field: SerializeField] public Mesh Detail { get; set; }

        [field: SerializeField] public Mesh Simple { get; set; }

        [field: SerializeField] public Mesh Impostor { get; set; }

        [field: SerializeField] public Material Bark { get; set; }

        [field: SerializeField] public Material Foliage { get; set; }

        [field: SerializeField] public Material ImpostorMaterial { get; set; }
    }
}
