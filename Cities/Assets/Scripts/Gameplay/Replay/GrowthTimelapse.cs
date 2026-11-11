using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Replay
{
    /// <summary>Een momentopname van de stad voor de replay (E22-08).</summary>
    public readonly struct GrowthFrame
    {
        public GrowthFrame(int day, int population, int buildings)
        {
            Day = day;
            Population = population;
            Buildings = buildings;
        }

        public int Day { get; }

        public int Population { get; }

        public int Buildings { get; }
    }

    /// <summary>
    /// Timelapse en replay (E22-08): bewaart om de zoveel dagen een frame van de groei en speelt ze versneld terug. Het aantal frames is
    /// begrensd; bij overschrijding wordt elk tweede frame weggelaten en het interval verdubbeld, zodat een lang spel klein blijft.
    /// </summary>
    public sealed class GrowthTimelapse
    {
        public const int MaxFrames = 256;

        private readonly List<GrowthFrame> _frames = new List<GrowthFrame>();

        public int IntervalDays { get; private set; } = 7;

        public IReadOnlyList<GrowthFrame> Frames => _frames;

        /// <summary>Neemt een frame op als de dag een veelvoud van het interval is.</summary>
        public bool Record(int day, int population, int buildings)
        {
            if (day < 0 || population < 0 || buildings < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(day), day, "Dag en aantallen zijn niet negatief.");
            }

            if (day % IntervalDays != 0 || (_frames.Count > 0 && _frames[_frames.Count - 1].Day >= day))
            {
                return false;
            }

            _frames.Add(new GrowthFrame(day, population, buildings));
            if (_frames.Count > MaxFrames)
            {
                Thin();
            }

            return true;
        }

        /// <summary>Het frame dat hoort bij een afspeelpositie van 0 tot 1.</summary>
        public GrowthFrame At(float progress)
        {
            if (_frames.Count == 0)
            {
                throw new InvalidOperationException("Er is nog niets opgenomen.");
            }

            var index = (int)Math.Round(Math.Min(1f, Math.Max(0f, progress)) * (_frames.Count - 1));
            return _frames[index];
        }

        private void Thin()
        {
            var kept = new List<GrowthFrame>();
            for (var i = 0; i < _frames.Count; i += 2)
            {
                kept.Add(_frames[i]);
            }

            _frames.Clear();
            _frames.AddRange(kept);
            IntervalDays *= 2;
        }
    }
}
