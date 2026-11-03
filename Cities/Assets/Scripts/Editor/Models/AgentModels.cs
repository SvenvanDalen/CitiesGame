using System.IO;
using Cities.Presentation.Agents;
using Cities.Simulation.Animation;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Hulp voor de geanimeerde agent-modellen (E21-05): materialen met de shader <c>Cities/CityAgent</c> en een renderer-prefab per model.
    /// Voertuigen krijgen draaiende wielen (hoekpunten getagd door <see cref="VehicleBuilder"/>); burgers een gebakken looppas
    /// (<see cref="CitizenBuilder"/>).
    /// </summary>
    internal static class AgentModels
    {
        internal const string Folder = "Assets/Art/Agents";

        internal static Material Material(string name, Color color, float smoothness, Texture2D clip = null, int frames = 1, int vertices = 1)
        {
            Directory.CreateDirectory(Folder);
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Cities/CityAgent"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (clip != null)
            {
                material.SetTexture("_ClipMap", clip);
                material.SetFloat("_ClipFrames", frames);
                material.SetFloat("_ClipVertices", vertices);
                material.SetFloat("_UseClip", 1f);
                material.EnableKeyword("_AGENT_CLIP");
            }
            else
            {
                material.SetFloat("_UseClip", 0f);
                material.DisableKeyword("_AGENT_CLIP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Prefab met een <see cref="AgentRendererBehaviour"/> die dit model met zijn materialen en animatie tekent.</summary>
        internal static GameObject RendererPrefab(string name, Mesh mesh, Material[] materials, AgentAnimationKind kind, float size)
        {
            Directory.CreateDirectory(Folder);
            var root = new GameObject(name + "Renderer");
            try
            {
                root.AddComponent<AgentRendererBehaviour>().Configure(mesh, materials, kind, size);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}Renderer.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Bakt een clip naar een textuur: één rij per frame, één texel per hoekpunt met de verplaatsing t.o.v. de rustpose.
        /// <paramref name="pose"/> geeft voor een hoekpunt en een fase (0..1) de verplaatsing.
        /// </summary>
        internal static Texture2D BakeClip(string name, int vertexCount, int frames, System.Func<int, float, float3> pose)
        {
            var layout = new BakedClipLayout(vertexCount, frames);
            var texture = new Texture2D(layout.VertexCount, layout.FrameCount, TextureFormat.RGBAHalf, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var texels = new Color[layout.VertexCount * layout.FrameCount];
            for (var frame = 0; frame < frames; frame++)
            {
                var phase = frame / (float)frames;
                for (var vertex = 0; vertex < vertexCount; vertex++)
                {
                    var offset = pose(vertex, phase);
                    texels[layout.TexelIndex(vertex, frame)] = new Color(offset.x, offset.y, offset.z, 0f);
                }
            }

            texture.SetPixels(texels);
            texture.Apply(false, false);
            var path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                Object.DestroyImmediate(texture);
                return existing;
            }

            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
