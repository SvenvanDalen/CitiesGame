using System.IO;
using System.Reflection;
using Cities.World.Buildings;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Maakt van <see cref="BuildingCatalog"/> de modellen, prefabs en <see cref="BuildingDefinition"/>-assets (E05-08). Opnieuw
    /// draaien werkt de bestaande assets bij en houdt de verwijzingen intact.
    /// </summary>
    internal static class BuildingAssets
    {
        private const string ArtFolder = "Assets/Art/Buildings";
        private const string DataFolder = "Assets/Data/Buildings";

        [MenuItem("Cities/Modellen/Gebouwvarianten bouwen")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(DataFolder);
            var materials = BuildingMaterials.Create();
            foreach (var design in BuildingCatalog.All)
            {
                var prefab = BuildPrefab(design, materials);
                WriteDefinition(design, prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildPrefab(BuildingDesign design, Material[] materials)
        {
            var folder = $"{ArtFolder}/{design.Name}";
            Directory.CreateDirectory(folder);
            var mesh = ModelMesh.Save(BuildingBuilder.BuildMesh(design), $"{folder}/{design.Name}Mesh.asset");
            var root = new GameObject(design.Name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                return PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{design.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WriteDefinition(BuildingDesign design, GameObject prefab)
        {
            var path = $"{DataFolder}/{design.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_id").stringValue = design.Id;
            so.FindProperty("_displayName").stringValue = design.Title;
            so.FindProperty("_description").stringValue = design.Description;
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            var spec = so.FindProperty("_spec");
            foreach (var field in typeof(BuildingSpec).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Set(spec.FindPropertyRelative(field.Name), field.GetValue(design.Spec));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void Set(SerializedProperty property, object value)
        {
            if (value is long l)
            {
                property.longValue = l;
            }
            else if (value is int i)
            {
                property.intValue = i;
            }
            else
            {
                property.enumValueIndex = (int)System.Convert.ChangeType(value, typeof(int));
            }
        }
    }
}
