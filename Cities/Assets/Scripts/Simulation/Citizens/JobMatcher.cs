using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>Een vacature (E07-05): een werkplek met vrije plekken en een minimale opleiding.</summary>
    public struct Vacancy
    {
        public EntityId<BuildingKind> Building;
        public float2 Position;
        public byte MinEducation;
        public int Slots;
    }

    /// <summary>
    /// Koppelt werkzoekenden aan vacatures (E07-05). De best opgeleiden mogen eerst kiezen (bij gelijke opleiding de laagste index, dus
    /// deterministisch); ieder neemt de dichtstbijzijnde vacature waarvoor hij genoeg opleiding heeft en die niet verder weg is dan
    /// <see cref="MaxDistance"/>. Wie niets vindt blijft werkloos.
    /// </summary>
    public static class JobMatcher
    {
        /// <summary>Verder dan dit (meter) gaat niemand voor een baan.</summary>
        public const float MaxDistance = 5000f;

        /// <summary>
        /// Zet <see cref="Citizen.Work"/> voor de gekoppelde werkzoekenden, verlaagt <see cref="Vacancy.Slots"/> en geeft het aantal
        /// geplaatsten terug. <paramref name="homes"/> is parallel aan <paramref name="citizens"/>.
        /// </summary>
        // shortcut: O(werkzoekenden x vacatures); upgrade naar een ruimtelijk raster bij tienduizenden vacatures.
        public static int Match(Citizen[] citizens, float2[] homes, Vacancy[] vacancies)
        {
            if (homes.Length != citizens.Length)
            {
                throw new ArgumentException("Elke burger heeft een thuispositie nodig.", nameof(homes));
            }

            var order = new List<int>();
            for (var i = 0; i < citizens.Length; i++)
            {
                if (citizens[i].IsJobSeeker)
                {
                    order.Add(i);
                }
            }

            order.Sort((a, b) =>
            {
                var byEducation = citizens[b].Education.CompareTo(citizens[a].Education);
                return byEducation != 0 ? byEducation : a.CompareTo(b);
            });

            var placed = 0;
            foreach (var seeker in order)
            {
                var best = Nearest(citizens[seeker].Education, homes[seeker], vacancies);
                if (best >= 0)
                {
                    citizens[seeker].Work = vacancies[best].Building;
                    vacancies[best].Slots--;
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Werkloosheid: het deel van de beroepsbevolking (volwassenen) zonder werk, van 0 tot 1.</summary>
        public static float UnemploymentRate(IReadOnlyList<Citizen> citizens)
        {
            int labour = 0, jobless = 0;
            for (var i = 0; i < citizens.Count; i++)
            {
                if (citizens[i].Group == AgeGroup.Adult)
                {
                    labour++;
                    jobless += citizens[i].HasWork ? 0 : 1;
                }
            }

            return labour == 0 ? 0f : (float)jobless / labour;
        }

        private static int Nearest(byte education, float2 from, Vacancy[] vacancies)
        {
            var best = -1;
            var bestSq = MaxDistance * MaxDistance;
            for (var v = 0; v < vacancies.Length; v++)
            {
                if (vacancies[v].Slots <= 0 || vacancies[v].MinEducation > education)
                {
                    continue;
                }

                var sq = math.distancesq(vacancies[v].Position, from);
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = v;
                }
            }

            return best;
        }
    }
}
