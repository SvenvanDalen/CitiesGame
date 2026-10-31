using System;
using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Unity.Mathematics;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// De rekenregels van de gebouwspawner (E05-04), los van kaart en wegennet: welke kant de voorkant op moet en welke
    /// kavels rond een cel passen. Een model heeft zijn voorkant aan lokaal -Z (zie <c>ASSET-GUIDELINES.md</c>), en
    /// <see cref="Footprint.QuarterTurns"/> draait met de klok mee.
    /// </summary>
    public static class BuildingPlacement
    {
        /// <summary>
        /// Het aantal kwartslagen waarmee de voorkant (lokaal -Z) naar <paramref name="towardRoad"/> (x, z) wijst:
        /// 0 is naar het zuiden, 1 naar het westen, 2 naar het noorden, 3 naar het oosten. Bij een schuine weg wint de langste as.
        /// </summary>
        public static int QuarterTurnsToFace(float2 towardRoad)
        {
            if (math.all(towardRoad == float2.zero))
            {
                return 0;
            }

            if (math.abs(towardRoad.x) > math.abs(towardRoad.y))
            {
                return towardRoad.x < 0f ? 1 : 3;
            }

            return towardRoad.y < 0f ? 0 : 2;
        }

        /// <summary>Alle kavels van <paramref name="width"/> × <paramref name="depth"/> cellen (breedte langs de weg) met deze draaiing waar <paramref name="anchor"/> in ligt.</summary>
        public static List<Footprint> Candidates(CellCoord anchor, int width, int depth, int quarterTurns)
        {
            var shape = new Footprint(default, width, depth, quarterTurns);
            var list = new List<Footprint>(shape.SizeX * shape.SizeZ);
            for (var dz = 0; dz < shape.SizeZ; dz++)
            {
                for (var dx = 0; dx < shape.SizeX; dx++)
                {
                    list.Add(new Footprint(new CellCoord(anchor.X - dx, anchor.Z - dz), width, depth, quarterTurns));
                }
            }

            return list;
        }

        /// <summary>De cellen van de kavel.</summary>
        public static IEnumerable<CellCoord> Cells(Footprint footprint)
        {
            for (var z = footprint.Origin.Z; z <= footprint.Max.Z; z++)
            {
                for (var x = footprint.Origin.X; x <= footprint.Max.X; x++)
                {
                    yield return new CellCoord(x, z);
                }
            }
        }

        /// <summary>Kiest een item met kans naar zijn gewicht; <paramref name="roll"/> ligt in [0, 1). Geeft <c>default</c> als alles gewicht 0 heeft.</summary>
        public static T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight, double roll)
        {
            var total = 0.0;
            foreach (var item in items)
            {
                total += Math.Max(0f, weight(item));
            }

            if (total <= 0.0)
            {
                return default;
            }

            var target = roll * total;
            foreach (var item in items)
            {
                target -= Math.Max(0f, weight(item));
                if (target < 0.0)
                {
                    return item;
                }
            }

            return items[items.Count - 1];
        }
    }
}
