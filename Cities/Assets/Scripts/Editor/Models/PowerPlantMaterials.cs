using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// De materialen van de centrales (E06-02): de gedeelde gebouwmaterialen, plus zonnepaneel en steenkool. De volgorde is die van
    /// <see cref="Slots"/>, dus ook van de submeshes van <see cref="PowerPlantBuilder"/>.
    /// </summary>
    internal static class PowerPlantMaterials
    {
        public const string Folder = "Assets/Art/Buildings/PowerPlants";

        public static class Slots
        {
            public const int Concrete = 0;
            public const int Cladding = 1;
            public const int Steel = 2;
            public const int White = 3;
            public const int Red = 4;
            public const int Glass = 5;
            public const int Stone = 6;
            public const int Asphalt = 7;
            public const int Door = 8;
            public const int Paving = 9;
            public const int SolarPanel = 10;
            public const int Coal = 11;
            public const int Count = 12;
        }

        private static readonly string[] Shared = { "Concrete", "Cladding", "Zinc", "Trim", "AccentRed", "Glass", "Stone", "Asphalt", "Door", "Paving" };

        public static Material[] Create()
        {
            // De gedeelde set bestaat pas na een keer bouwen; BuildingMaterials.Create maakt hem aan en laadt hem anders gewoon.
            BuildingMaterials.Create();
            var result = new Material[Slots.Count];
            for (var i = 0; i < Shared.Length; i++)
            {
                result[i] = AssetDatabase.LoadAssetAtPath<Material>($"{BuildingMaterials.Folder}/{Shared[i]}.mat");
            }

            System.IO.Directory.CreateDirectory($"{Folder}/Textures");
            var panel = ModelMaterials.SaveTexture($"{Folder}/Textures/SolarPanel.png", SolarCells());
            var coal = ModelMaterials.SaveTexture($"{Folder}/Textures/Coal.png", CoalLumps());
            result[Slots.SolarPanel] = ModelMaterials.Lit(Folder, "SolarPanel", panel, Color.white, 0.85f, 0.3f, Vector2.one);
            result[Slots.Coal] = ModelMaterials.Lit(Folder, "Coal", coal, Color.white, 0.2f, 0f, new Vector2(0.5f, 0.5f));
            return result;
        }

        /// <summary>Monokristallijn paneel: 6 × 10 donkerblauwe cellen in een zilveren raster, met een witte rand van het frame.</summary>
        private static Color[] SolarCells()
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x * 6 % size;
                    var cy = y * 10 % size;
                    var grid = cx < 10 || cy < 10;
                    var busbar = Mathf.Abs((cx / (float)size * 6f) - 0.5f) < 0.02f;
                    var tone = new Color(0.05f, 0.09f, 0.2f);
                    var shade = 1f + (ModelMaterials.Noise(x / 8, y / 8, 91) * 0.08f);
                    pixels[(y * size) + x] = grid ? new Color(0.62f, 0.64f, 0.67f) : ModelMaterials.Shade(busbar ? new Color(0.45f, 0.5f, 0.6f) : tone, shade);
                }
            }

            return pixels;
        }

        /// <summary>Losse brokken steenkool: zwart met glanzende vlakken.</summary>
        private static Color[] CoalLumps()
        {
            var size = ModelMaterials.Size;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var lump = Mathf.PerlinNoise((x * 0.06f) + 5f, (y * 0.06f) + 5f);
                    var fine = Mathf.PerlinNoise((x * 0.3f) + 9f, (y * 0.3f) + 9f);
                    pixels[(y * size) + x] = ModelMaterials.Shade(new Color(0.09f, 0.09f, 0.1f), 0.5f + (lump * 0.9f) + (fine * 0.5f));
                }
            }

            return pixels;
        }
    }
}
