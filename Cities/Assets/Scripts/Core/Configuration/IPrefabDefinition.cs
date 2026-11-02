using UnityEngine;

namespace Cities.Core.Configuration
{
    /// <summary>Een definitie met een model (E21-03). <see cref="DefinitionDatabase"/> vindt daarmee het prefab van elk gebouw, voertuig en elke centrale op ID.</summary>
    public interface IPrefabDefinition : IDefinition
    {
        /// <summary>Model voor rendering; mag ontbreken zolang er geen model is.</summary>
        GameObject Prefab { get; }
    }
}
