using Cities.World.Buildings;
using UnityEngine;

namespace Cities.Presentation.Buildings
{
    /// <summary>
    /// Bouwt bij het starten een gebouw uit een footprint (E21-06) en zet het als mesh op dit object. De materialen zijn in de
    /// volgorde muur, raam, deur, dak.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralBuildingBehaviour : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _footprintWidth = 4;
        [SerializeField, Min(1)] private int _footprintDepth = 3;
        [SerializeField, Min(0.5f)] private float _cellSize = 8f;
        [SerializeField, Range(1, ProceduralBuilding.MaxFloors)] private int _floors = 3;
        [SerializeField] private RoofStyle _roof = RoofStyle.Gabled;
        [SerializeField] private ulong _seed = 1;

        private Mesh _mesh;

        private void Start() => Rebuild();

        [ContextMenu("Opnieuw bouwen")]
        public void Rebuild()
        {
            var data = ProceduralBuildingMesh.Build(new ProceduralBuildingSpec(_footprintWidth, _footprintDepth, _cellSize, _floors, _roof, _seed));
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ProceduralBuilding" };
            }

            _mesh.Clear();
            _mesh.SetVertices(data.Vertices);
            _mesh.SetNormals(data.Normals);
            _mesh.SetUVs(0, data.Uvs);
            _mesh.subMeshCount = BuildingMeshData.SubmeshCount;
            for (var i = 0; i < BuildingMeshData.SubmeshCount; i++)
            {
                _mesh.SetTriangles(data.Submeshes[i], i);
            }

            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }
}
