using Cities.World.Buildings;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Editor.Configuration
{
    /// <summary>Menu "Cities/Definities/Gebouwmodellen valideren": controleert van elk gebouw het model tegen het sjabloon.</summary>
    public static class BuildingPrefabValidator
    {
        private const float CellSize = 8f;

        [MenuItem("Cities/Definities/Gebouwmodellen valideren")]
        public static void ValidateAll()
        {
            var problems = 0;
            var definitions = AssetDatabase.FindAssets("t:" + nameof(BuildingDefinition))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingDefinition>)
                .Where(d => d != null && d.Prefab != null);
            var count = 0;
            foreach (var definition in definitions)
            {
                count++;
                var errors = new List<string>();
                var renderers = definition.Prefab.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    var root = definition.Prefab.transform.position;
                    var min = bounds.min - root;
                    var max = bounds.max - root;
                    BuildingTemplate.Validate(definition.Spec, (min.x, min.y, min.z), (max.x, max.y, max.z), renderers.Length, CellSize, errors);
                }
                else
                {
                    BuildingTemplate.Validate(definition.Spec, default, default, 0, CellSize, errors);
                }

                foreach (var error in errors)
                {
                    problems++;
                    Debug.LogWarning($"Gebouwmodel {definition.Id}: {error}", definition);
                }
            }

            Debug.Log($"Gebouwmodellen: {count} gecontroleerd, {problems} problemen.");
        }
    }
}
