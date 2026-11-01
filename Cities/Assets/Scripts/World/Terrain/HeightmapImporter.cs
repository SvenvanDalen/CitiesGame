using System;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Leest hoogtekaarten uit bestandsinhoud: PNG (via Unity; 8-bit grijs, rode kanaal) en RAW (16-bit). De bestanden zelf leest de aanroeper in,
    /// zodat dit zonder pad of bestandssysteem te gebruiken is.
    /// </summary>
    public static class HeightmapImporter
    {
        /// <summary>Leest een PNG; de rode waarde van elke pixel is de hoogte. De onderste rij van de afbeelding is het zuiden.</summary>
        public static Heightmap FromPng(byte[] png, float cellSize, float heightScale, float waterLevel)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!texture.LoadImage(png))
                {
                    throw new ArgumentException("De PNG is niet te lezen.", nameof(png));
                }

                var pixels = texture.GetPixels32();
                var gray = new byte[pixels.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    gray[i] = pixels[i].r;
                }

                return Heightmap.FromGray8(gray, texture.width, texture.height, cellSize, heightScale, waterLevel);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
