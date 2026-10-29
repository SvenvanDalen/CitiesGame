using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cities.Core.Configuration;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Configuration
{
    /// <summary>
    /// Houdt <c>Assets/Data/DefinitionCatalog.asset</c> automatisch gelijk aan alle definitie-assets in het project
    /// en meldt validatiefouten in de console.
    /// </summary>
    public sealed class DefinitionCatalogBuilder : AssetPostprocessor
    {
        public const string CatalogPath = "Assets/Data/DefinitionCatalog.asset";

        [MenuItem("Cities/Definities/Catalogus bijwerken en valideren")]
        public static void RebuildAndValidate()
        {
            var database = Rebuild().CreateDatabase();
            if (database.IsValid)
            {
                Debug.Log($"Definitiecatalogus: {database.Count} definities, geen fouten.");
                return;
            }

            foreach (var error in database.Errors)
            {
                Debug.LogWarning($"Definitie: {error}");
            }
        }

        /// <summary>Zoekt alle definities en schrijft ze naar de catalogus; maakt de catalogus aan als die ontbreekt.</summary>
        public static DefinitionCatalog Rebuild()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath) ?? "Assets/Data");
                catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var definitions = AssetDatabase.FindAssets("t:" + nameof(GameDefinition))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameDefinition>)
                .Where(d => d != null)
                .ToList();

            if (!SameContents(catalog.Definitions, definitions))
            {
                catalog.SetDefinitions(definitions);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            return catalog;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom);
            if (touched.Any(IsDefinitionPath))
            {
                // Pas na de import bijwerken; tijdens de import mag de AssetDatabase niet worden gewijzigd.
                EditorApplication.delayCall += () => Rebuild();
            }
        }

        private static bool IsDefinitionPath(string path) =>
            path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase) && path != CatalogPath &&
            (AssetDatabase.GetMainAssetTypeAtPath(path)?.IsSubclassOf(typeof(GameDefinition)) ?? path.StartsWith("Assets/Data/"));

        private static bool SameContents(IReadOnlyList<GameDefinition> current, List<GameDefinition> found) =>
            current.Count == found.Count && !found.Except(current).Any();
    }
}
