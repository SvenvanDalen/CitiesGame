using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Cities.Gameplay.Statistics;
using Cities.Simulation.Disasters;
using Cities.Simulation.Economy;
using Cities.Simulation.Milestones;
using Unity.Mathematics;

namespace Cities.Gameplay.Events
{
    /// <summary>
    /// Rampen, mijlpalen en meldingen van de stad (E26-12). Elke dag schuiven de rampen op (schade gaat van het saldo), komen nieuwe
    /// rampen vanzelf op een willekeurige plek, worden bevolkingsmijlpalen met een beloning toegekend en komt alles in de meldingenlijst.
    /// </summary>
    public sealed class CityEvents
    {
        public const double SecondsPerDay = 86_400.0;

        private readonly ulong _seed;

        public CityEvents(ulong seed)
        {
            _seed = seed;
            Disasters = new DisasterManager();
            Disasters.Register(Earthquake.Spec);
            Disasters.Register(Flood.RiverSpec);
            Disasters.Register(Flood.TsunamiSpec);
            Disasters.Register(Meteorite.Spec);
            Disasters.Register(Sinkhole.Spec);
            Disasters.Register(Storm.Spec);
            Disasters.Register(Tornado.Spec);
            Milestones = new MilestoneTracker();
            Notifications = new NotificationCenter(0.0);
            Disasters.PhaseChanged += OnPhase;
        }

        /// <summary>Zoveel dagen na de start ontstaan er geen rampen vanzelf, zodat een nieuwe stad eerst kan groeien.</summary>
        public const int GraceDays = 90;

        public DisasterManager Disasters { get; }

        public MilestoneTracker Milestones { get; }

        public NotificationCenter Notifications { get; }

        /// <summary>Totale schade van alle rampen tot nu toe.</summary>
        public long TotalDamage { get; private set; }

        /// <summary>De huidige dag voor de tijdstempels van meldingen.</summary>
        public int Day { get; private set; }

        /// <summary>Eén spelsdag. <paramref name="area"/> is de kaartgrootte in meters.</summary>
        public void Daily(int day, int week, int population, Ledger ledger, float2 area)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            Day = day;
            var random = new DeterministicRandom(_seed, (ulong)day);
            var damage = Disasters.Step(ref random, area, day >= GraceDays);
            if (damage > 0)
            {
                TotalDamage += damage;
                ledger.Force(week, LedgerCategory.Other, damage);
            }

            foreach (var achievement in Milestones.Update(population, day))
            {
                ledger.Earn(week, LedgerCategory.Other, achievement.Milestone.RewardCash);
                Post("milestone." + achievement.Milestone.Id, Severity.Info, achievement.Milestone.Message, null);
            }
        }

        /// <summary>De nieuwste meldingen, nieuwste eerst.</summary>
        public IReadOnlyList<Notification> Latest(int count)
        {
            var items = Notifications.Items;
            var result = new List<Notification>();
            for (var i = items.Count - 1; i >= 0 && result.Count < count; i--)
            {
                result.Add(items[i]);
            }

            return result;
        }

        private void OnPhase(Disaster disaster)
        {
            var severity = disaster.Phase == DisasterPhase.Ended ? Severity.Info : Severity.Critical;
            Post("disaster." + disaster.Spec.Name + "." + disaster.Phase + "." + Day, severity, Describe(disaster), disaster.Position);
        }

        private static string Describe(Disaster disaster)
        {
            var name = disaster.Spec.Name;
            switch (disaster.Phase)
            {
                case DisasterPhase.Warning: return name + " op komst";
                case DisasterPhase.Active: return name + " treft de stad";
                case DisasterPhase.Aftermath: return name + " is voorbij, het herstel begint";
                default: return name + " is helemaal voorbij";
            }
        }

        private void Post(string key, Severity severity, string text, float2? location) =>
            Notifications.Post(key, severity, text, Day * SecondsPerDay, location);
    }
}
