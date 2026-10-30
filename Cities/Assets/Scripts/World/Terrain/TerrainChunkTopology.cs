using System;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Vaste opbouw van een chunkmesh met <c>n</c> vlakjes per zijde: eerst het raster van (n+1)² hoekpunten rij voor
    /// rij, daarna een rok (skirt) van 4 × (n+1) hoekpunten die recht onder de randen hangen. De rok sluit de kieren
    /// tussen chunks met een verschillend detailniveau; de driehoeken zijn voor elke chunk op dat niveau gelijk.
    /// </summary>
    public static class TerrainChunkTopology
    {
        public static int GridVertexCount(int resolution) => (resolution + 1) * (resolution + 1);

        public static int VertexCount(int resolution) => GridVertexCount(resolution) + (4 * (resolution + 1));

        public static int IndexCount(int resolution) => (resolution * resolution * 6) + (4 * resolution * 6);

        /// <summary>Het rasterpunt waar rokpunt <paramref name="skirt"/> (0 tot 4·(n+1)) onder hangt.</summary>
        public static int SkirtSource(int resolution, int skirt)
        {
            var perEdge = resolution + 1;
            if (skirt < 0 || skirt >= 4 * perEdge)
            {
                throw new ArgumentOutOfRangeException(nameof(skirt), skirt, "Rokpunt valt buiten de chunk.");
            }

            var edge = skirt / perEdge;
            var i = skirt % perEdge;
            return EdgeVertex(resolution, edge, i);
        }

        /// <summary>
        /// Driehoeken met de klok mee van buitenaf gezien (Unity's voorkant): het raster wijst omhoog, de rok naar buiten.
        /// </summary>
        public static ushort[] BuildIndices(int resolution)
        {
            if (resolution < 1 || VertexCount(resolution) > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolutie past niet in 16-bits indices.");
            }

            var indices = new ushort[IndexCount(resolution)];
            var n = resolution + 1;
            var k = 0;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var a = (z * n) + x;
                    var b = a + n;
                    var c = b + 1;
                    var d = a + 1;
                    k = Quad(indices, k, a, b, c, d);
                }
            }

            var skirtStart = GridVertexCount(resolution);
            for (var edge = 0; edge < 4; edge++)
            {
                for (var i = 0; i < resolution; i++)
                {
                    var top0 = EdgeVertex(resolution, edge, i);
                    var top1 = EdgeVertex(resolution, edge, i + 1);
                    var bottom0 = skirtStart + (edge * n) + i;
                    k = Quad(indices, k, bottom0, top0, top1, bottom0 + 1);
                }
            }

            return indices;
        }

        /// <summary>
        /// Rasterpunt <paramref name="i"/> langs een rand, van links naar rechts gezien van buiten de chunk:
        /// 0 = zuid (oplopende x), 1 = oost (oplopende z), 2 = noord (aflopende x), 3 = west (aflopende z).
        /// </summary>
        internal static int EdgeVertex(int resolution, int edge, int i)
        {
            var n = resolution + 1;
            switch (edge)
            {
                case 0:
                    return i;
                case 1:
                    return (i * n) + resolution;
                case 2:
                    return (resolution * n) + (resolution - i);
                default:
                    return (resolution - i) * n;
            }
        }

        private static int Quad(ushort[] indices, int k, int a, int b, int c, int d)
        {
            indices[k++] = (ushort)a;
            indices[k++] = (ushort)b;
            indices[k++] = (ushort)c;
            indices[k++] = (ushort)a;
            indices[k++] = (ushort)c;
            indices[k++] = (ushort)d;
            return k;
        }
    }
}
