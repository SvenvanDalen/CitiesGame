using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Problems
{
    /// <summary>Een icoon dat getekend moet worden: wereldpositie en soort.</summary>
    public readonly struct ProblemIcon
    {
        public ProblemIcon(float3 position, BuildingProblem problem)
        {
            Position = position;
            Problem = problem;
        }

        public float3 Position { get; }

        public BuildingProblem Problem { get; }
    }

    /// <summary>Camera en grenzen voor <see cref="ProblemIconLayout.Build"/>.</summary>
    public readonly struct ProblemIconView
    {
        public ProblemIconView(float3 eye, float3 right, float iconSize, float lift, float maxDistance, int maxIcons)
        {
            Eye = eye;
            Right = right;
            IconSize = iconSize;
            Lift = lift;
            MaxDistance = maxDistance;
            MaxIcons = maxIcons;
        }

        public float3 Eye { get; }

        /// <summary>Rechtervector van de camera.</summary>
        public float3 Right { get; }

        public float IconSize { get; }

        /// <summary>Hoogte boven het dak.</summary>
        public float Lift { get; }

        public float MaxDistance { get; }

        public int MaxIcons { get; }
    }

    /// <summary>Kiest en rangschikt de iconen boven gebouwen: dichtbij, begrensd in aantal, naast elkaar gecentreerd.</summary>
    public static class ProblemIconLayout
    {
        /// <summary>Afstand tussen de iconen van één gebouw, als veelvoud van de iconbreedte.</summary>
        public const float Spacing = 1.1f;

        /// <summary>Offset langs de camera-rechtervector voor icoon <paramref name="index"/> van <paramref name="count"/>.</summary>
        public static float Offset(int index, int count, float iconSize)
        {
            if (count <= 0 || index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (index - ((count - 1) * 0.5f)) * iconSize * Spacing;
        }

        /// <summary>
        /// Zet voor elk gebouw binnen <paramref name="maxDistance"/> zijn iconen boven het dak (+ <paramref name="lift"/>), de dichtstbijzijnde
        /// gebouwen eerst, en stopt bij <paramref name="maxIcons"/>. <paramref name="right"/> is de rechtervector van de camera.
        /// </summary>
        public static void Build(IReadOnlyList<KeyValuePair<int, ProblemSet>> buildings, Func<int, float3> roofOf,
            in ProblemIconView view, List<ProblemIcon> into)
        {
            var (eye, right, iconSize, lift, maxDistance, maxIcons) = (view.Eye, view.Right, view.IconSize, view.Lift, view.MaxDistance, view.MaxIcons);
            into.Clear();
            var candidates = new List<(float Distance, int Index)>();
            for (var i = 0; i < buildings.Count; i++)
            {
                var distance = math.distance(roofOf(buildings[i].Key), eye);
                if (distance <= maxDistance)
                {
                    candidates.Add((distance, i));
                }
            }

            candidates.Sort((a, b) =>
            {
                var byDistance = a.Distance.CompareTo(b.Distance);
                return byDistance != 0 ? byDistance : buildings[a.Index].Key.CompareTo(buildings[b.Index].Key);
            });

            var kinds = new BuildingProblem[ProblemSet.KindCount];
            foreach (var candidate in candidates)
            {
                var building = buildings[candidate.Index];
                var count = building.Value.CopyTo(kinds);
                if (into.Count + count > maxIcons)
                {
                    break;
                }

                var roof = roofOf(building.Key) + new float3(0f, lift, 0f);
                for (var k = 0; k < count; k++)
                {
                    into.Add(new ProblemIcon(roof + (right * Offset(k, count, iconSize)), kinds[k]));
                }
            }
        }
    }
}
