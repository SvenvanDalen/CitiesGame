using System;
using System.Collections.Generic;
using Cities.Simulation.Services;
using Cities.Simulation.Utilities;

namespace Cities.Simulation.Problems
{
    /// <summary>Vertaalt de toestand van bestaande systemen naar problemen voor de <see cref="ProblemTracker"/>.</summary>
    public static class ProblemSources
    {
        public static void ReportFire(ProblemTracker tracker, FireModel fire, int building)
        {
            Require(tracker, fire);
            tracker.Report(building, BuildingProblem.Fire, fire.StateOf(building) == FireState.Burning);
        }

        /// <summary>Een afvalbak is vol als hij overloopt of de ophaalmelding open staat.</summary>
        public static void ReportBin(ProblemTracker tracker, WasteBin bin, int building)
        {
            Require(tracker, bin);
            tracker.Report(building, BuildingProblem.GarbageFull, bin.Overflow > 0f || bin.PickupRequested);
        }

        /// <summary>Gebouwen in een afgeschakeld district (zie <see cref="PowerShortage.Shed"/>) hebben geen stroom.</summary>
        public static void ReportPower(ProblemTracker tracker, int building, int district, ISet<int> dark)
        {
            Require(tracker, dark);
            tracker.Report(building, BuildingProblem.NoPower, dark.Contains(district));
        }

        /// <summary>Zonder water in de toren staat het net droog.</summary>
        public static void ReportWater(ProblemTracker tracker, WaterTower tower, int building)
        {
            Require(tracker, tower);
            tracker.Report(building, BuildingProblem.NoWater, tower.Fill <= 0f);
        }

        private static void Require(ProblemTracker tracker, object source)
        {
            if (tracker is null)
            {
                throw new ArgumentNullException(nameof(tracker));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
        }
    }
}
