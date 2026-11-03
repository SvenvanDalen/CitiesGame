using System;
using System.Collections.Generic;
using Cities.Simulation.Network;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Knipt een gebogen weg in rechte linten, want een <see cref="OverlayEntityValue"/> is een rechte band.</summary>
    public static class CurveRibbons
    {
        /// <summary>Lengte in meters waarboven een curve in meerdere linten wordt gesplitst.</summary>
        public const float MaxPieceLength = 25f;

        /// <summary>Voegt de curve toe als één of meer linten met dezelfde waarde; geeft het aantal linten terug.</summary>
        public static int Add(List<OverlayEntityValue> output, ulong id, Bezier3 curve, float width, float value)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var pieces = Math.Max(1, (int)Math.Ceiling(curve.Length() / MaxPieceLength));
            var previous = curve.Position(0f);
            for (var i = 1; i <= pieces; i++)
            {
                var next = curve.Position(i / (float)pieces);
                output.Add(new OverlayEntityValue(id, previous, next, width, value));
                previous = next;
            }

            return pieces;
        }
    }
}
