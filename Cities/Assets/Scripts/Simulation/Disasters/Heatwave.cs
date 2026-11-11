using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Hitte en droogte (E15-14). Een hittegolf duurt dagen en wordt per dag droger: het waterverbruik en de energievraag (koeling) stijgen,
    /// en de brandstof op brandbare cellen neemt toe, zodat een brand zich sneller verspreidt. Bij zware droogte kan op zeer droge
    /// cellen vanzelf een brand ontstaan. Alles volgt uit de seed. De effecten zijn bewust zacht (hooguit +60 % water, +40 % energie).
    /// </summary>
    public sealed class Heatwave
    {
        public const int MaxStrength = 5;

        public const float WaterPerStrength = 0.12f;

        public const float EnergyPerStrength = 0.08f;

        /// <summary>Brandstof die een cel per dag erbij krijgt, per krachtpunt en bij volle droogte.</summary>
        public const float FuelPerStrength = 0.02f;

        /// <summary>Alleen cellen met minstens zoveel brandstof kunnen vanzelf ontbranden.</summary>
        public const float IgnitionFuel = 0.8f;

        /// <summary>Kans per dag per krachtpunt dat een droge cel vanzelf ontbrandt, bij volle droogte.</summary>
        public const float IgnitionPerStrength = 0.002f;

        public Heatwave(int id, int strength, int durationDays, ulong seed)
        {
            if (strength < 1 || strength > MaxStrength)
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "De kracht ligt tussen 1 en 5.");
            }

            if (durationDays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(durationDays), durationDays, "Een hittegolf duurt minstens een dag.");
            }

            Id = id;
            Strength = strength;
            DurationDays = durationDays;
            Seed = seed;
        }

        /// <summary>Een hittegolf wordt twee dagen van tevoren voorspeld en duurt een week.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Hitte en droogte", 0.0005f, 2, 7, 5, 8_000, 2_000);

        public int Id { get; }

        public int Strength { get; }

        public int DurationDays { get; }

        public ulong Seed { get; }

        public int AgeDays { get; private set; }

        public bool IsOver => AgeDays >= DurationDays;

        /// <summary>Hoe droog het is (0 tot 1): loopt op over de dagen en is aan het einde maximaal.</summary>
        public float Dryness => math.saturate((AgeDays + 1f) / DurationDays);

        /// <summary>Vermenigvuldiger op het waterverbruik van de stad.</summary>
        public float WaterDemandFactor => 1f + (WaterPerStrength * Strength * Dryness);

        /// <summary>Vermenigvuldiger op de energievraag (koeling).</summary>
        public float EnergyDemandFactor => 1f + (EnergyPerStrength * Strength);

        /// <summary>
        /// Eén dag droogte: brandstof neemt toe en zeer droge cellen kunnen vanzelf ontbranden. Geeft het aantal nieuwe branden.
        /// <paramref name="fire"/> mag null zijn.
        /// </summary>
        public int Advance(FireSpread fire)
        {
            var day = (ulong)AgeDays;
            var dryness = Dryness;
            AgeDays++;
            if (fire == null)
            {
                return 0;
            }

            var ignitions = 0;
            var chance = IgnitionPerStrength * Strength * dryness;
            for (var y = 0; y < fire.Height; y++)
            {
                for (var x = 0; x < fire.Width; x++)
                {
                    var fuel = fire.FuelAt(x, y);
                    if (fuel <= 0f)
                    {
                        continue;
                    }

                    fire.SetFuel(x, y, fuel + (FuelPerStrength * Strength * dryness));
                    var cell = (ulong)((y * fire.Width) + x);
                    var roll = (SeedHash.Combine(Seed, day, cell) >> 40) / (float)(1 << 24);
                    if (fuel >= IgnitionFuel && !fire.IsBurning(x, y) && roll < chance)
                    {
                        fire.Ignite(x, y);
                        ignitions++;
                    }
                }
            }

            return ignitions;
        }

        internal void Restore(int ageDays) => AgeDays = ageDays;
    }
}
