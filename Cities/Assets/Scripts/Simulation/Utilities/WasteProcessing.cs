using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    public enum WasteMethod
    {
        Landfill = 0,
        Incinerator = 1,
        Recycling = 2,
        Compost = 3,
    }

    /// <summary>Wat één verwerkingsmethode kost en oplevert, per ton gemengd afval dat hij aanneemt.</summary>
    public readonly struct WasteProfile
    {
        public WasteProfile(float accepted, float cost, float revenue, float energyMwh, float groundPollution, float airPollution, float residue)
        {
            Accepted = accepted;
            Cost = cost;
            Revenue = revenue;
            EnergyMwh = energyMwh;
            GroundPollution = groundPollution;
            AirPollution = airPollution;
            Residue = residue;
        }

        /// <summary>Deel van het gemengde afval dat de methode kan verwerken (0 tot 1); de rest blijft liggen.</summary>
        public float Accepted { get; }

        /// <summary>Kosten in euro per verwerkte ton.</summary>
        public float Cost { get; }

        /// <summary>Opbrengst in euro per verwerkte ton (grondstoffen, compost).</summary>
        public float Revenue { get; }

        /// <summary>Opgewekte energie in megawattuur per verwerkte ton.</summary>
        public float EnergyMwh { get; }

        public float GroundPollution { get; }

        public float AirPollution { get; }

        /// <summary>Deel van de verwerkte massa dat overblijft (as, resten) en op de stortplaats belandt.</summary>
        public float Residue { get; }
    }

    /// <summary>Uitkomst van één verwerking, in ton, euro en vervuilingseenheden.</summary>
    public readonly struct WasteResult
    {
        public WasteResult(float processed, float notAccepted, WasteProfile profile)
        {
            Processed = processed;
            NotAccepted = notAccepted;
            Cost = processed * profile.Cost;
            Revenue = processed * profile.Revenue;
            EnergyMwh = processed * profile.EnergyMwh;
            GroundPollution = processed * profile.GroundPollution;
            AirPollution = processed * profile.AirPollution;
            Residue = processed * profile.Residue;
        }

        public float Processed { get; }

        /// <summary>Wat de installatie niet kon of mocht aannemen (te weinig capaciteit, of een soort die hij niet verwerkt).</summary>
        public float NotAccepted { get; }

        public float Cost { get; }

        public float Revenue { get; }

        public float EnergyMwh { get; }

        public float GroundPollution { get; }

        public float AirPollution { get; }

        /// <summary>Restmassa die nog gestort moet worden.</summary>
        public float Residue { get; }
    }

    /// <summary>
    /// Afvalverwerking (E06-10): stortplaats, verbrandingsoven, recycling en composteerinstallatie. De stortplaats neemt alles aan maar
    /// vervuilt de grond; de oven neemt alles aan, wekt stroom op en vervuilt de lucht; recycling en compost nemen maar een deel van het
    /// gemengde afval aan, kosten meer en leveren geld op.
    /// </summary>
    public static class WasteProcessing
    {
        public static WasteProfile Profile(WasteMethod method)
        {
            switch (method)
            {
                case WasteMethod.Incinerator:
                    return new WasteProfile(accepted: 1f, cost: 60f, revenue: 0f, energyMwh: 0.6f, groundPollution: 0f, airPollution: 0.05f, residue: 0.1f);
                case WasteMethod.Recycling:
                    return new WasteProfile(accepted: 0.35f, cost: 90f, revenue: 140f, energyMwh: 0f, groundPollution: 0f, airPollution: 0f, residue: 0.2f);
                case WasteMethod.Compost:
                    return new WasteProfile(accepted: 0.3f, cost: 30f, revenue: 35f, energyMwh: 0f, groundPollution: 0f, airPollution: 0.01f, residue: 0.3f);
                default:
                    return new WasteProfile(accepted: 1f, cost: 20f, revenue: 0f, energyMwh: 0f, groundPollution: 0.02f, airPollution: 0f, residue: 1f);
            }
        }

        /// <summary>
        /// Verwerkt <paramref name="tonnes"/> gemengd afval met een installatie die per stap <paramref name="capacity"/> ton aankan. De methode
        /// neemt zijn <see cref="WasteProfile.Accepted"/> deel aan, begrensd door de capaciteit.
        /// </summary>
        public static WasteResult Process(WasteMethod method, float tonnes, float capacity)
        {
            if (!(tonnes >= 0f) || !(capacity >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes), "Hoeveelheid en capaciteit moeten nul of meer zijn.");
            }

            var profile = Profile(method);
            var processed = math.min(tonnes * profile.Accepted, capacity);
            return new WasteResult(processed, tonnes - processed, profile);
        }
    }
}
