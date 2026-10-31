using System;

namespace Cities.World.Water
{
    /// <summary>
    /// Eén punt van het wateroppervlak. De volgorde van de velden is de vertexindeling van de mesh:
    /// positie (3 floats), dan diepte en stroomrichting (3 floats, texcoord0).
    /// </summary>
    public readonly struct WaterVertex
    {
        public WaterVertex(float x, float y, float z, float depth, float flowX, float flowZ)
        {
            X = x;
            Y = y;
            Z = z;
            Depth = depth;
            FlowX = flowX;
            FlowZ = flowZ;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        /// <summary>Waterdiepte in meters; 0 aan de oever, waar de shader het water laat uitlopen.</summary>
        public float Depth { get; }

        /// <summary>Stroomrichting op het oppervlak (x, z), lengte 0 tot 1: 1 is een snelle stroom.</summary>
        public float FlowX { get; }

        public float FlowZ { get; }
    }

    /// <summary>
    /// Bouwt het wateroppervlak van één blok cellen uit hoogte en waterdiepte op de hoekpunten. Alleen cellen met
    /// water (op minstens één hoekpunt) krijgen een vlak; droge hoekpunten liggen op het maaiveld met diepte 0, zodat
    /// de oever zacht uitloopt. De stroomrichting is het verval van het oppervlak.
    /// </summary>
    public sealed class WaterMeshBuilder
    {
        /// <summary>Cellen per zijde van een blok; 33 × 33 punten blijven binnen 16-bit indices.</summary>
        public const int BlockCells = 32;

        /// <summary>Onder deze diepte (m) telt een punt als droog.</summary>
        public const float WetThreshold = 0.02f;

        /// <summary>Verval (m per m) waarbij de stroom volle snelheid heeft.</summary>
        public const float FullFlowSlope = 0.01f;

        private const int Side = BlockCells + 1;

        private readonly WaterVertex[] _vertices = new WaterVertex[Side * Side];
        private readonly ushort[] _indices = new ushort[BlockCells * BlockCells * 6];

        public ReadOnlySpan<WaterVertex> Vertices => _vertices.AsSpan(0, Side * Side);

        public ReadOnlySpan<ushort> Indices => _indices.AsSpan(0, IndexCount);

        public int IndexCount { get; private set; }

        /// <summary>
        /// Vult de mesh voor het blok dat bij hoekpunt (<paramref name="cornerX"/>, <paramref name="cornerZ"/>) begint.
        /// Geeft <c>false</c> als er geen water in het blok ligt.
        /// </summary>
        /// <param name="ground">Terreinhoogte per hoekpunt, rij voor rij.</param>
        /// <param name="water">Waterdiepte per hoekpunt, rij voor rij.</param>
        /// <param name="width">Hoekpunten per rij.</param>
        /// <param name="depth">Rijen hoekpunten.</param>
        /// <param name="spacing">Afstand tussen hoekpunten in meters.</param>
        public bool Build(ReadOnlySpan<float> ground, ReadOnlySpan<float> water, int width, int depth, float spacing, int cornerX, int cornerZ)
        {
            IndexCount = 0;
            var anyWet = false;
            for (var z = 0; z < Side; z++)
            {
                for (var x = 0; x < Side; x++)
                {
                    var cx = Math.Min(cornerX + x, width - 1);
                    var cz = Math.Min(cornerZ + z, depth - 1);
                    var i = (cz * width) + cx;
                    var d = water[i] > WetThreshold ? water[i] : 0f;
                    anyWet |= d > 0f;
                    var flow = Flow(ground, water, width, depth, cx, cz, spacing);
                    _vertices[(z * Side) + x] = new WaterVertex(cx * spacing, ground[i] + d, cz * spacing, d, flow.X, flow.Z);
                }
            }

            if (!anyWet)
            {
                return false;
            }

            for (var z = 0; z < BlockCells; z++)
            {
                for (var x = 0; x < BlockCells; x++)
                {
                    var a = (z * Side) + x;
                    var b = a + 1;
                    var c = a + Side;
                    var d = c + 1;
                    if (_vertices[a].Depth + _vertices[b].Depth + _vertices[c].Depth + _vertices[d].Depth <= 0f)
                    {
                        continue;
                    }

                    Quad(a, b, c, d);
                }
            }

            return IndexCount > 0;
        }

        private void Quad(int a, int b, int c, int d)
        {
            _indices[IndexCount++] = (ushort)a;
            _indices[IndexCount++] = (ushort)c;
            _indices[IndexCount++] = (ushort)b;
            _indices[IndexCount++] = (ushort)b;
            _indices[IndexCount++] = (ushort)c;
            _indices[IndexCount++] = (ushort)d;
        }

        private static (float X, float Z) Flow(ReadOnlySpan<float> ground, ReadOnlySpan<float> water, int width, int depth, int x, int z, float spacing)
        {
            var west = Surface(ground, water, width, Math.Max(x - 1, 0), z);
            var east = Surface(ground, water, width, Math.Min(x + 1, width - 1), z);
            var south = Surface(ground, water, width, x, Math.Max(z - 1, 0));
            var north = Surface(ground, water, width, x, Math.Min(z + 1, depth - 1));
            var fx = -(east - west) / (2f * spacing * FullFlowSlope);
            var fz = -(north - south) / (2f * spacing * FullFlowSlope);
            var length = MathF.Sqrt((fx * fx) + (fz * fz));
            return length > 1f ? (fx / length, fz / length) : (fx, fz);
        }

        private static float Surface(ReadOnlySpan<float> ground, ReadOnlySpan<float> water, int width, int x, int z)
        {
            var i = (z * width) + x;
            return ground[i] + (water[i] > WetThreshold ? water[i] : 0f);
        }
    }
}
