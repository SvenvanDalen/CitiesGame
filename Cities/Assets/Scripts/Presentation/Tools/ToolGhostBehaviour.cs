using System.Linq;
using Cities.Gameplay.Tools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Presentation.Tools
{
    /// <summary>
    /// Tekent de preview van de actieve tool: het model doorschijnend en de footprint op de grond, blauw als het mag
    /// en rood als het niet mag. Voor de <see cref="TerraformTool"/> tekent de terreinshader de penseelrand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolGhostBehaviour : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Brush = Shader.PropertyToID("_CityBrush");

        [SerializeField] private ToolsBehaviour _tools;

        [Tooltip("Doorschijnend materiaal voor het model (URP Lit, Transparent).")]
        [SerializeField] private Material _ghostMaterial;

        [Tooltip("Doorschijnend materiaal voor de footprint (URP Unlit, Transparent).")]
        [SerializeField] private Material _footprintMaterial;

        [SerializeField] private Color _valid = new Color(0.35f, 0.72f, 1f, 0.5f);
        [SerializeField] private Color _invalid = new Color(1f, 0.22f, 0.18f, 0.55f);

        [Tooltip("Hoogte van de footprint boven de basis, tegen z-fighting met de grond.")]
        [SerializeField] private float _footprintLift = 0.05f;

        private MaterialPropertyBlock _block;
        private Mesh _quad;
        private GameObject _source;
        private GameObject _ghost;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _quad = CreateQuad();
        }

        private void LateUpdate()
        {
            var controller = _tools.Controller;
            var preview = controller.Preview;
            UseModel(controller.Active?.Ghost);
            var brushState = preview.IsValid ? 1f : -1f;
            Shader.SetGlobalVector(Brush, controller.Active is TerraformTool brush && preview.IsVisible
                ? new Vector4(preview.Position.x, preview.Position.z, brush.Radius, brushState)
                : Vector4.zero);
            if (!preview.IsVisible)
            {
                SetGhostVisible(false);
                return;
            }

            _block.SetColor(BaseColor, preview.IsValid ? _valid : _invalid);
            var rotation = Quaternion.Euler(0f, preview.YawDegrees, 0f);
            Vector3 position = preview.Position;
            if (_ghost != null)
            {
                _ghost.transform.SetPositionAndRotation(position, rotation);
                foreach (var ghostRenderer in _renderers)
                {
                    ghostRenderer.SetPropertyBlock(_block);
                }

                SetGhostVisible(true);
            }

            if (_footprintMaterial != null && preview.Size.x > 0f)
            {
                var matrix = Matrix4x4.TRS(position + (Vector3.up * _footprintLift), rotation, new Vector3(preview.Size.x, 1f, preview.Size.y));
                Graphics.RenderMesh(new RenderParams(_footprintMaterial) { matProps = _block, shadowCastingMode = ShadowCastingMode.Off }, _quad, 0, matrix);
            }
        }

        private void OnDestroy()
        {
            Shader.SetGlobalVector(Brush, Vector4.zero);
            if (_ghost != null)
            {
                Destroy(_ghost);
            }

            Destroy(_quad);
        }

        /// <summary>Maakt een kopie van het model zonder colliders of scripts, met het ghostmateriaal op elk deel.</summary>
        private void UseModel(GameObject model)
        {
            if (model == _source)
            {
                return;
            }

            if (_ghost != null)
            {
                Destroy(_ghost);
            }

            _source = model;
            _ghost = null;
            _renderers = System.Array.Empty<Renderer>();
            if (model == null)
            {
                return;
            }

            _ghost = Instantiate(model, transform);
            _ghost.name = $"Ghost {model.name}";
            foreach (var component in _ghost.GetComponentsInChildren<Component>(true).Where(c => c is Collider || c is MonoBehaviour))
            {
                Destroy(component);
            }

            _renderers = _ghost.GetComponentsInChildren<Renderer>(true);
            foreach (var ghostRenderer in _renderers)
            {
                var materials = new Material[ghostRenderer.sharedMaterials.Length];
                System.Array.Fill(materials, _ghostMaterial);
                ghostRenderer.sharedMaterials = materials;
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            _ghost.SetActive(false);
        }

        private void SetGhostVisible(bool visible)
        {
            if (_ghost != null && _ghost.activeSelf != visible)
            {
                _ghost.SetActive(visible);
            }
        }

        /// <summary>Vlak van 1 × 1 m in het grondvlak, met het midden op de oorsprong.</summary>
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Tool footprint" };
            mesh.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
