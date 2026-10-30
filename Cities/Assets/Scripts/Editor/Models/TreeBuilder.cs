using System.Collections.Generic;
using System.IO;
using Cities.Presentation.Trees;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt de boommodellen voor <see cref="TreeModelSet"/>: een loofboom van zo'n 17 m (beukachtig, met stam,
    /// gesteltakken, twijgen en bladtrossen) en een fijnspar van 22 m (kransen van hangende takken). Elk in drie
    /// detailniveaus: het volle model, een eenvoudige kroon en een impostor met foto's van het volle model van opzij en
    /// van boven. Alles volgt uit een vaste seed.
    /// </summary>
    internal static class TreeBuilder
    {
        private const string Folder = "Assets/Art/Trees";
        private const int CaptureLayer = 31;
        private const int CaptureSize = 512;

        [MenuItem("Cities/Modellen/Bomen bouwen")]
        public static void Build()
        {
            Directory.CreateDirectory($"{Folder}/Textures");
            var broadleafBark = TreeTextures.Save($"{Folder}/Textures/BroadleafBark.png", TreeTextures.Size, TreeTextures.Size, TreeTextures.BroadleafBark(), false);
            var coniferBark = TreeTextures.Save($"{Folder}/Textures/ConiferBark.png", TreeTextures.Size, TreeTextures.Size, TreeTextures.ConiferBark(), false);
            var leaves = TreeTextures.Save($"{Folder}/Textures/Leaves.png", TreeTextures.Size, TreeTextures.Size, TreeTextures.LeafAtlas(), true);
            var needles = TreeTextures.Save($"{Folder}/Textures/SpruceBranch.png", TreeTextures.Size, TreeTextures.Size / 2, TreeTextures.ConiferBranch(), true);

            var broadleaf = Species("Broadleaf", BroadleafShape.Build, broadleafBark, leaves, 0.12f);
            var conifer = Species("Conifer", ConiferShape.Build, coniferBark, needles, 0.1f);

            var setPath = $"{Folder}/Trees.asset";
            var set = AssetDatabase.LoadAssetAtPath<TreeModelSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<TreeModelSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }

            set.Species = new[] { broadleaf, conifer };
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"Bomen gebouwd: {Describe(broadleaf)}; {Describe(conifer)}");
        }

        private static string Describe(TreeModel model) =>
            $"{model.Detail.name} {Triangles(model.Detail)}/{Triangles(model.Simple)}/{Triangles(model.Impostor)} driehoeken";

        private static int Triangles(Mesh mesh)
        {
            var count = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                count += (int)mesh.GetIndexCount(i) / 3;
            }

            return count;
        }

        private static TreeModel Species(string name, System.Action<TreeMesh, bool> shape, Texture2D barkTexture, Texture2D foliageTexture, float smoothness)
        {
            var bark = Lit($"{name}Bark", barkTexture, smoothness, false, new Vector2(1f, 1f));
            var foliage = Lit($"{name}Foliage", foliageTexture, 0.22f, true, Vector2.one);

            var detail = new TreeMesh();
            shape(detail, true);
            var simple = new TreeMesh();
            shape(simple, false);
            var detailMesh = SaveMesh(detail.Build($"{name}Detail"));
            var simpleMesh = SaveMesh(simple.Build($"{name}Simple"));

            var bounds = detailMesh.bounds;
            var side = Mathf.Max(bounds.size.y, bounds.size.x, bounds.size.z);
            var crown = Mathf.Max(bounds.size.x, bounds.size.z);
            var atlas = Capture($"{Folder}/Textures/{name}Impostor.png", detailMesh, new[] { bark, foliage }, bounds, side, crown);
            var impostorMaterial = ImpostorMaterial($"{name}Impostor", atlas);
            var impostor = SaveMesh(ImpostorMesh($"{name}Impostor", bounds, side, crown, WidestFoliage(detailMesh)));

            return new TreeModel
            {
                Detail = detailMesh,
                Simple = simpleMesh,
                Impostor = impostor,
                Bark = bark,
                Foliage = foliage,
                ImpostorMaterial = impostorMaterial,
            };
        }

        /// <summary>
        /// Twee gekruiste staande vlakken (foto van opzij) en één liggend vlak (foto van boven) op de hoogte waar de
        /// kroon het breedst is, zodat het van opzij binnen het silhouet blijft.
        /// </summary>
        private static Mesh ImpostorMesh(string name, Bounds bounds, float side, float crown, float crownHeight)
        {
            var mesh = new TreeMesh();
            var bottom = bounds.center.y - (side * 0.5f);
            var sideUv = new Rect(0f, 0f, 0.5f, 1f);
            foreach (var axis in new[] { Vector3.right, Vector3.forward })
            {
                var normal = Vector3.Cross(axis, Vector3.up);
                var lit = ((normal * 0.4f) + Vector3.up).normalized;
                mesh.Card(0, new Vector3(0f, bottom + (side * 0.5f), 0f), axis * side, Vector3.up * side, lit, sideUv);
            }

            mesh.Card(0, new Vector3(0f, crownHeight, 0f), Vector3.right * crown, Vector3.forward * crown, Vector3.up, new Rect(0.5f, 0f, 0.5f, 1f));
            return mesh.Build(name, 1);
        }

        /// <summary>Hoogte (in stappen van 1 m) waarop het blad het verst van de stam reikt.</summary>
        private static float WidestFoliage(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var widest = new Dictionary<int, float>();
            foreach (var index in mesh.GetTriangles(1))
            {
                var vertex = vertices[index];
                var band = Mathf.FloorToInt(vertex.y);
                widest[band] = Mathf.Max(widest.GetValueOrDefault(band), new Vector2(vertex.x, vertex.z).magnitude);
            }

            var best = 0;
            foreach (var (band, reach) in widest)
            {
                if (reach > widest.GetValueOrDefault(best))
                {
                    best = band;
                }
            }

            return best + 0.5f;
        }

        /// <summary>
        /// Fotografeert het volle model zonder licht (alleen de kleur) van opzij en van boven, met een transparante
        /// achtergrond. De bladkleur wordt iets donkerder genomen, omdat de impostor geen schaduw binnen de kroon heeft.
        /// </summary>
        private static Texture2D Capture(string path, Mesh mesh, Material[] materials, Bounds bounds, float side, float crown)
        {
            var origin = new Vector3(0f, -20000f, 0f);
            var unlit = new Material[materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                unlit[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                unlit[i].SetTexture("_BaseMap", materials[i].GetTexture("_BaseMap"));
                unlit[i].SetTextureScale("_BaseMap", materials[i].GetTextureScale("_BaseMap"));
                unlit[i].SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.78f, 1f));
                unlit[i].SetFloat("_AlphaClip", 1f);
                unlit[i].SetFloat("_Cutoff", 0.5f);
                unlit[i].SetFloat("_Cull", 0f);
                unlit[i].EnableKeyword("_ALPHATEST_ON");
            }

            var model = new GameObject("Tree capture") { hideFlags = HideFlags.HideAndDontSave, layer = CaptureLayer };
            model.transform.position = origin;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = unlit;

            var cameraObject = new GameObject("Tree capture camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.cullingMask = 1 << CaptureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.24f, 0.1f, 0f);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 400f;
            camera.enabled = false;

            var atlas = new Texture2D(CaptureSize * 2, CaptureSize, TextureFormat.RGBA32, false);

            // Anders tekent de editor nog niet gecompileerde shaders onzichtbaar en blijft de foto leeg.
            var asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var center = origin + bounds.center;
                camera.orthographicSize = side * 0.5f;
                camera.transform.SetPositionAndRotation(center - (Vector3.forward * 200f), Quaternion.identity);
                Shoot(camera, atlas, 0);

                camera.orthographicSize = crown * 0.5f;
                camera.transform.SetPositionAndRotation(origin + new Vector3(0f, bounds.max.y + 200f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                Shoot(camera, atlas, CaptureSize);

                File.WriteAllBytes(path, atlas.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(cameraObject);
                foreach (var material in unlit)
                {
                    Object.DestroyImmediate(material);
                }
            }

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Shoot(Camera camera, Texture2D atlas, int offsetX)
        {
            var target = RenderTexture.GetTemporary(new RenderTextureDescriptor(CaptureSize, CaptureSize, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                atlas.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), offsetX, 0);
                RenderTexture.active = previous;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Material ImpostorMaterial(string name, Texture2D atlas)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Cities/TreeImpostor"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Cities/TreeImpostor");
            material.SetTexture("_BaseMap", atlas);
            material.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Lit(string name, Texture2D texture, float smoothness, bool alphaClip, Vector2 tiling)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", alphaClip ? 0f : 2f);
            if (alphaClip)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh) => ModelMesh.Save(mesh, $"{Folder}/{mesh.name}.asset");

        /// <summary>Mesh met vloeiende buizen (bast, submesh 0) en kaarten (blad, submesh 1).</summary>
        internal sealed class TreeMesh
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _triangles = { new List<int>(), new List<int>() };

            /// <summary>
            /// Buis langs <paramref name="points"/>: u loopt één keer rond, v in eenheden van
            /// <paramref name="metersPerRepeat"/> langs de lengte.
            /// </summary>
            public void Tube(IReadOnlyList<Vector3> points, IReadOnlyList<float> radii, int sides, float metersPerRepeat)
            {
                var start = _vertices.Count;
                var forward = (points[1] - points[0]).normalized;
                var right = Vector3.Cross(forward, Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                var v = 0f;
                for (var i = 0; i < points.Count; i++)
                {
                    var next = points[Mathf.Min(i + 1, points.Count - 1)];
                    var previous = points[Mathf.Max(i - 1, 0)];
                    var tangent = (next - previous).normalized;

                    // Parallel transport: het assenkruis draait mee zonder te tollen.
                    right = Vector3.ProjectOnPlane(right, tangent).normalized;
                    var up = Vector3.Cross(tangent, right);
                    if (i > 0)
                    {
                        v += Vector3.Distance(points[i], points[i - 1]) / metersPerRepeat;
                    }

                    for (var s = 0; s <= sides; s++)
                    {
                        var angle = s * Mathf.PI * 2f / sides;
                        var direction = (Mathf.Cos(angle) * right) + (Mathf.Sin(angle) * up);
                        _vertices.Add(points[i] + (direction * radii[i]));
                        _normals.Add(direction);
                        _uvs.Add(new Vector2(s / (float)sides, v));
                    }
                }

                var ring = sides + 1;
                for (var i = 0; i < points.Count - 1; i++)
                {
                    for (var s = 0; s < sides; s++)
                    {
                        var a = start + (i * ring) + s;
                        var b = a + ring;
                        _triangles[0].AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
                }
            }

            /// <summary>Rechthoek rond <paramref name="center"/> met zijden <paramref name="width"/> en <paramref name="height"/>.</summary>
            public void Card(int submesh, Vector3 center, Vector3 width, Vector3 height, Vector3 normal, Rect uv)
            {
                Quad(submesh, center - (width * 0.5f) - (height * 0.5f), center + (width * 0.5f) - (height * 0.5f), center + (width * 0.5f) + (height * 0.5f), center - (width * 0.5f) + (height * 0.5f), normal, uv);
            }

            /// <summary>Vierhoek a-b-c-d met UV (min.x, min.y) bij a en (max.x, max.y) bij c.</summary>
            public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Rect uv)
            {
                var start = _vertices.Count;
                _vertices.AddRange(new[] { a, b, c, d });
                _normals.AddRange(new[] { normal, normal, normal, normal });
                _uvs.AddRange(new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) });
                _triangles[submesh].AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            public Mesh Build(string name, int submeshes = 2)
            {
                var mesh = new Mesh { name = name, subMeshCount = submeshes };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                for (var i = 0; i < submeshes; i++)
                {
                    mesh.SetTriangles(_triangles[i], i);
                }

                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        /// <summary>Herhaalbare reeks getallen voor de vorm van een boom.</summary>
        internal sealed class ShapeRandom
        {
            private uint _state;

            public ShapeRandom(uint seed) => _state = (seed * 2654435761u) + 1u;

            public float Range(float min, float max)
            {
                unchecked
                {
                    _state ^= _state << 13;
                    _state ^= _state >> 17;
                    _state ^= _state << 5;
                    return min + ((max - min) * ((_state & 0xFFFFFF) / 16777216f));
                }
            }

            public Vector3 OnSphere()
            {
                var z = Range(-1f, 1f);
                var angle = Range(0f, Mathf.PI * 2f);
                var r = Mathf.Sqrt(1f - (z * z));
                return new Vector3(r * Mathf.Cos(angle), z, r * Mathf.Sin(angle));
            }
        }
    }
}
