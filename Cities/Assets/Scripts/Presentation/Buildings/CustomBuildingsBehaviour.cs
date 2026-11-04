using System.Collections.Generic;
using System.IO;
using Cities.Gameplay.Zoning;
using Cities.World.Buildings;
using UnityEngine;

namespace Cities.Presentation.Buildings
{
    /// <summary>
    /// Laadt eigen gebouwen uit <c>persistentDataPath/Buildings</c> (E21-08): per submap een <c>building.txt</c> en een OBJ-model.
    /// Het bouwt voor elk een prefab en meldt de definities aan bij <see cref="CustomBuildingRegistry"/> voordat de bouwer start.
    /// Problemen met een map komen als waarschuwing in het logboek; de rest laadt gewoon.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CustomBuildingsBehaviour : MonoBehaviour
    {
        public const string FolderName = "Buildings";

        [SerializeField, Min(0.5f)] private float _cellSize = 8f;
        [Tooltip("Materiaal voor modellen; leeg gebruikt een grijs URP Lit-materiaal.")]
        [SerializeField] private Material _material;

        private readonly List<Object> _owned = new List<Object>();

        private void Awake()
        {
            var folder = Path.Combine(Application.persistentDataPath, FolderName);
            var errors = new List<string>();
            var definitions = new List<BuildingDefinition>();
            foreach (var building in CustomBuildingLibrary.LoadFolder(folder, _cellSize, errors))
            {
                definitions.Add(BuildingDefinition.CreateRuntime(building.Manifest.Id, building.Manifest.Name, building.Manifest.Spec, BuildPrefab(building)));
            }

            foreach (var error in errors)
            {
                Debug.LogWarning($"Eigen gebouw: {error}");
            }

            CustomBuildingRegistry.Replace(definitions);
            _owned.AddRange(definitions);
        }

        private GameObject BuildPrefab(CustomBuilding building)
        {
            var model = building.Model;
            var mesh = new Mesh { name = building.Manifest.Id };
            mesh.SetVertices(model.Vertices);
            mesh.SetNormals(model.Normals);
            mesh.SetUVs(0, model.Uvs);
            mesh.subMeshCount = model.Submeshes.Count;
            for (var i = 0; i < model.Submeshes.Count; i++)
            {
                mesh.SetTriangles(model.Submeshes[i], i);
            }

            mesh.RecalculateBounds();
            var material = _material != null ? _material : new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.75f, 0.75f, 0.75f) };
            var materials = new Material[model.Submeshes.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            // De sjabloon staat ver buiten beeld; de bouwer zet elke kopie op zijn plek.
            var template = new GameObject(building.Manifest.Id) { hideFlags = HideFlags.HideInHierarchy };
            template.transform.SetParent(transform, false);
            template.transform.localPosition = new Vector3(0f, -10000f, 0f);
            template.AddComponent<MeshFilter>().sharedMesh = mesh;
            template.AddComponent<MeshRenderer>().sharedMaterials = materials;
            _owned.Add(mesh);
            _owned.Add(template);
            if (_material == null)
            {
                _owned.Add(material);
            }

            return template;
        }

        private void OnDestroy()
        {
            CustomBuildingRegistry.Replace(new BuildingDefinition[0]);
            foreach (var owned in _owned)
            {
                if (owned != null)
                {
                    Destroy(owned);
                }
            }
        }
    }
}
