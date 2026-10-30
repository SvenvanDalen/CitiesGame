using System.IO;
using Cities.Presentation.Roads;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt de texturen en materialen van het wegdek voor <see cref="RoadMaterialSet"/>, in de volgorde van
    /// <c>RoadSurface</c>: asfalt, fietspad, stoep, stoeprand, doorgetrokken en onderbroken streep. Alles volgt uit vaste seeds.
    /// </summary>
    internal static class RoadBuilder
    {
        private const string Folder = "Assets/Art/Roads";
        private const string BaseMap = "_BaseMap";
        private const string BumpMap = "_BumpMap";

        [MenuItem("Cities/Modellen/Wegen bouwen")]
        public static void Build()
        {
            Directory.CreateDirectory($"{Folder}/Textures");
            var (asphalt, asphaltHeight) = RoadTextures.Asphalt(new Color(0.3f, 0.3f, 0.32f), 101);
            var (bike, bikeHeight) = RoadTextures.Asphalt(new Color(0.72f, 0.29f, 0.23f), 211);
            var (paving, pavingHeight) = RoadTextures.Paving(307);
            const int width = RoadTextures.AsphaltWidth;
            const int depth = RoadTextures.AsphaltHeight;
            const int pavingWidth = RoadTextures.PavingTilesAcross * RoadTextures.PavingTilePixels;
            const int pavingDepth = RoadTextures.PavingTilesAlong * RoadTextures.PavingTilePixels;

            var materials = new[]
            {
                Lit("Asphalt", Texture("Asphalt", width, depth, asphalt), Normal("Asphalt", width, depth, asphaltHeight, 2.5f), 0.12f),
                Lit("Bike", Texture("Bike", width, depth, bike), Normal("Bike", width, depth, bikeHeight, 2.5f), 0.14f),
                Lit("Sidewalk", Texture("Paving", pavingWidth, pavingDepth, paving), Normal("Paving", pavingWidth, pavingDepth, pavingHeight, 4f), 0.1f),
                Lit("Curb", Texture("Concrete", 128, 512, RoadTextures.Concrete(128, 512, 401)), null, 0.08f),
                Paint("MarkingSolid", RoadTextures.Save($"{Folder}/Textures/PaintSolid.png", 16, 512, RoadTextures.Paint(16, 512, 1f, 503), TextureKind.Paint)),
                Paint("MarkingDashed", RoadTextures.Save($"{Folder}/Textures/PaintDashed.png", 16, 512, RoadTextures.Paint(16, 512, 0.375f, 607), TextureKind.Paint)),
            };

            var path = $"{Folder}/Roads.asset";
            var set = AssetDatabase.LoadAssetAtPath<RoadMaterialSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<RoadMaterialSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.Set(materials);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("Wegen gebouwd: 6 materialen in " + path);
        }

        private static Texture2D Texture(string name, int width, int height, Color[] pixels) =>
            RoadTextures.Save($"{Folder}/Textures/{name}.png", width, height, pixels, TextureKind.Color);

        private static Texture2D Normal(string name, int width, int height, float[] heights, float strength) =>
            RoadTextures.Save($"{Folder}/Textures/{name}Normal.png", width, height, RoadTextures.Normals(heights, width, height, strength), TextureKind.Normal);

        private static Material Lit(string name, Texture2D color, Texture2D normal, float smoothness)
        {
            var material = Asset(name);
            material.SetTexture(BaseMap, color);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetTexture(BumpMap, normal);
            material.SetFloat("_BumpScale", 1f);
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Verf: een gesneden (alpha-tested) laag zonder glans.</summary>
        private static Material Paint(string name, Texture2D texture)
        {
            var material = Lit(name, texture, null, 0.3f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Asset(string name)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }
    }
}
