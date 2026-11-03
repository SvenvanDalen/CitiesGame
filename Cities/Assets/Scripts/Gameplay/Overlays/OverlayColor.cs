using System;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Kleur met acht bits per kanaal, zonder Unity-types zodat de logica ook buiten de editor te testen is.</summary>
    public readonly struct OverlayColor : IEquatable<OverlayColor>
    {
        public OverlayColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }

        /// <summary>Lineaire menging; <paramref name="t"/> wordt begrensd tot 0 tot 1.</summary>
        public static OverlayColor Lerp(OverlayColor from, OverlayColor to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return new OverlayColor(Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t), Mix(from.A, to.A, t));
        }

        /// <summary>Vier bytes in de volgorde van een RGBA32-textuur (R in de laagste byte).</summary>
        public uint Pack() => R | ((uint)G << 8) | ((uint)B << 16) | ((uint)A << 24);

        public bool Equals(OverlayColor other) => Pack() == other.Pack();

        public override bool Equals(object obj) => obj is OverlayColor other && Equals(other);

        public override int GetHashCode() => (int)Pack();

        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";

        private static byte Mix(byte a, byte b, float t) => (byte)Math.Round(a + ((b - a) * t));
    }
}
