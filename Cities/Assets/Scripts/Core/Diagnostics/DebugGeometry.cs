using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Core.Diagnostics
{
    /// <summary>Kleuren van debuglijnen en -labels; de presentatie zet ze om naar echte kleuren.</summary>
    public enum DebugColor : byte
    {
        Neutral,
        Accent,
        Good,
        Warning,
        Bad,
        Info,
    }

    /// <summary>Lijnstuk in wereldcoördinaten (meters).</summary>
    public readonly struct DebugLine
    {
        public DebugLine(float3 from, float3 to, DebugColor color)
        {
            From = from;
            To = to;
            Color = color;
        }

        public float3 From { get; }

        public float3 To { get; }

        public DebugColor Color { get; }
    }

    /// <summary>Tekst op een punt in de wereld, bijvoorbeeld een ID boven een gebouw.</summary>
    public readonly struct DebugLabel
    {
        public DebugLabel(float3 position, string text, DebugColor color)
        {
            Position = position;
            Text = text ?? string.Empty;
            Color = color;
        }

        public float3 Position { get; }

        public string Text { get; }

        public DebugColor Color { get; }
    }

    /// <summary>
    /// Wat een debug-overlay deze frame tekent. Heeft vaste bovengrenzen, zodat een overlay op een grote stad het
    /// beeld niet laat vastlopen; wat erboven komt telt mee in <see cref="DroppedLines"/> en <see cref="DroppedLabels"/>.
    /// </summary>
    public sealed class DebugGeometry
    {
        private readonly List<DebugLine> _lines;
        private readonly List<DebugLabel> _labels;

        public DebugGeometry(int maxLines = 65536, int maxLabels = 512)
        {
            if (maxLines < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLines), maxLines, "Kan niet negatief zijn.");
            }

            if (maxLabels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLabels), maxLabels, "Kan niet negatief zijn.");
            }

            MaxLines = maxLines;
            MaxLabels = maxLabels;
            _lines = new List<DebugLine>(Math.Min(maxLines, 4096));
            _labels = new List<DebugLabel>(Math.Min(maxLabels, 256));
        }

        public int MaxLines { get; }

        public int MaxLabels { get; }

        public IReadOnlyList<DebugLine> Lines => _lines;

        public IReadOnlyList<DebugLabel> Labels => _labels;

        public int DroppedLines { get; private set; }

        public int DroppedLabels { get; private set; }

        public void AddLine(float3 from, float3 to, DebugColor color = DebugColor.Neutral)
        {
            if (_lines.Count < MaxLines)
            {
                _lines.Add(new DebugLine(from, to, color));
            }
            else
            {
                DroppedLines++;
            }
        }

        public void AddLabel(float3 position, string text, DebugColor color = DebugColor.Neutral)
        {
            if (_labels.Count < MaxLabels)
            {
                _labels.Add(new DebugLabel(position, text, color));
            }
            else
            {
                DroppedLabels++;
            }
        }

        public void Clear()
        {
            _lines.Clear();
            _labels.Clear();
            DroppedLines = 0;
            DroppedLabels = 0;
        }
    }

    /// <summary>Wat de camera ziet; overlays tekenen alleen in de buurt van <see cref="Focus"/>.</summary>
    public readonly struct DebugView
    {
        public DebugView(float3 cameraPosition, float3 focus, float radius)
        {
            CameraPosition = cameraPosition;
            Focus = focus;
            Radius = radius;
        }

        public float3 CameraPosition { get; }

        /// <summary>Punt op de grond waar de camera naar kijkt.</summary>
        public float3 Focus { get; }

        /// <summary>Straal in meters rond <see cref="Focus"/> waarin overlays tekenen.</summary>
        public float Radius { get; }

        /// <summary>Hoogte van de camera boven het focuspunt; bepaalt het detail.</summary>
        public float Altitude => CameraPosition.y - Focus.y;
    }
}
