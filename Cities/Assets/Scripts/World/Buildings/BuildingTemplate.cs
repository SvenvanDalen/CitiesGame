using System;
using System.Collections.Generic;

namespace Cities.World.Buildings
{
    /// <summary>
    /// Het gebouw-sjabloon (E21-02): de afspraken waaraan elk gebouwmodel moet voldoen. De oorsprong is het midden van de kavel op maaiveld, de
    /// voorkant ligt aan lokaal -Z en het model past binnen zijn footprint in cellen. De controle werkt op de omhullende van het model, zodat
    /// ze zonder Unity te testen is; de editor-menuopdracht levert de omhullende van een prefab aan.
    /// </summary>
    public static class BuildingTemplate
    {
        /// <summary>Hoeveel meter een model buiten zijn kavel mag steken (dakrand, schoorsteen, luifel).</summary>
        public const float Overhang = 0.5f;

        /// <summary>Hoeveel meter de onderkant van het model van het maaiveld mag afwijken.</summary>
        public const float GroundTolerance = 0.2f;

        /// <summary>Het model moet minstens dit deel van de kavelbreedte en -diepte vullen, anders is het model of de footprint verkeerd.</summary>
        public const float MinFill = 0.5f;

        /// <summary>
        /// Controleert een model met omhullende <paramref name="min"/>–<paramref name="max"/> (x, y, z in meter, lokaal) tegen de footprint in
        /// <paramref name="spec"/>. Problemen komen in <paramref name="errors"/>.
        /// </summary>
        public static void Validate(BuildingSpec spec, (float X, float Y, float Z) min, (float X, float Y, float Z) max, int renderers, float cellSize, ICollection<string> errors)
        {
            if (errors is null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (renderers < 1)
            {
                errors.Add("Het model heeft geen renderer.");
                return;
            }

            var halfX = spec.FootprintWidth * cellSize * 0.5f;
            var halfZ = spec.FootprintDepth * cellSize * 0.5f;
            if (min.X < -halfX - Overhang || max.X > halfX + Overhang || min.Z < -halfZ - Overhang || max.Z > halfZ + Overhang)
            {
                errors.Add($"Het model steekt buiten de footprint van {spec.FootprintWidth}×{spec.FootprintDepth} cellen (oorsprong moet het midden van de kavel zijn).");
            }

            if (max.X - min.X < halfX * 2f * MinFill || max.Z - min.Z < halfZ * 2f * MinFill)
            {
                errors.Add("Het model vult minder dan de helft van de footprint.");
            }

            if (Math.Abs(min.Y) > GroundTolerance)
            {
                errors.Add($"De onderkant van het model ligt {min.Y:0.##} m van het maaiveld; de oorsprong hoort op de grond.");
            }

            if (max.Y <= min.Y)
            {
                errors.Add("Het model heeft geen hoogte.");
            }
        }
    }
}
