using System;
using System.Collections.Generic;
using Cities.Simulation.Services;
using Unity.Mathematics;

namespace Cities.Gameplay.Services
{
    public enum CityService
    {
        Fire,
        Police,
        Health,
        Education,
    }

    /// <summary>
    /// Stadsdiensten op de kaart (E26-07): brandweer, politie, zorg en onderwijs met een dekkingsstraal. Gebouwen binnen de straal zijn
    /// gedekt; het aandeel gedekte diensten is de dienstenscore die migratie beïnvloedt. Brandweer, politie en zorg zijn ook
    /// <see cref="ServiceStation"/>s voor het budget; onderwijs kost alleen onderhoud.
    /// </summary>
    public sealed class CityServices
    {
        public const int KindCount = 4;

        private readonly List<(CityService Kind, float2 Position)> _placed = new List<(CityService, float2)>();

        public int Count => _placed.Count;

        public IReadOnlyList<(CityService Kind, float2 Position)> Placed => _placed;

        public static float RadiusOf(CityService kind)
        {
            switch (kind)
            {
                case CityService.Fire: return 350f;
                case CityService.Police: return 300f;
                case CityService.Health: return 400f;
                default: return 500f;
            }
        }

        public static long UpkeepPerMonthOf(CityService kind)
        {
            switch (kind)
            {
                case CityService.Fire: return 18_000;
                case CityService.Police: return 15_000;
                case CityService.Health: return 25_000;
                default: return 20_000;
            }
        }

        public static string NameOf(CityService kind)
        {
            switch (kind)
            {
                case CityService.Fire: return "Brandweer";
                case CityService.Police: return "Politie";
                case CityService.Health: return "Kliniek";
                default: return "School";
            }
        }

        /// <summary>Plaatst een dienst; geeft het budgetstation terug voor brandweer, politie en zorg, anders <c>null</c>.</summary>
        public ServiceStation Place(CityService kind, float2 position)
        {
            _placed.Add((kind, position));
            switch (kind)
            {
                case CityService.Fire: return new ServiceStation(ServiceKind.Fire, position, RadiusOf(kind), 3, UpkeepPerMonthOf(kind));
                case CityService.Police: return new ServiceStation(ServiceKind.Police, position, RadiusOf(kind), 3, UpkeepPerMonthOf(kind));
                case CityService.Health: return new ServiceStation(ServiceKind.Health, position, RadiusOf(kind), 3, UpkeepPerMonthOf(kind));
                default: return null;
            }
        }

        public bool IsCovered(CityService kind, float2 point)
        {
            var radius = RadiusOf(kind);
            foreach (var (placedKind, position) in _placed)
            {
                if (placedKind == kind && math.lengthsq(point - position) <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Aandeel van de vier diensten dat dit punt dekt, 0 tot 1.</summary>
        public float CoverageAt(float2 point)
        {
            var covered = 0;
            for (var i = 0; i < KindCount; i++)
            {
                if (IsCovered((CityService)i, point))
                {
                    covered++;
                }
            }

            return covered / (float)KindCount;
        }

        /// <summary>Gemiddelde dekking over een aantal punten; 0,5 (neutraal) zonder punten.</summary>
        public float AverageCoverage(IReadOnlyList<float2> points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count == 0)
            {
                return 0.5f;
            }

            var sum = 0f;
            foreach (var point in points)
            {
                sum += CoverageAt(point);
            }

            return sum / points.Count;
        }
    }
}
