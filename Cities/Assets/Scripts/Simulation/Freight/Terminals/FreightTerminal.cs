using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Freight.Terminals
{
    /// <summary>
    /// Een haven of railterminal (E14-06): slaat goederen op en draagt ze over tussen schip of trein
    /// en vrachtwagen, begrensd door ligplaatsen, kraansnelheid, poortcapaciteit en opslag.
    /// </summary>
    public sealed class FreightTerminal
    {
        public const float Epsilon = 0.0001f;
        public const int MaxCarriers = 4096;

        private readonly List<TerminalCarrier> _carriers = new List<TerminalCarrier>();
        private int _nextCarrierId;

        internal FreightTerminal(int id, ulong owner, float3 position, TerminalSpec spec)
        {
            Id = id;
            Owner = owner;
            Position = position;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public int Id { get; }

        /// <summary>Het gebouw (verpakt ID) waar de terminal bij hoort.</summary>
        public ulong Owner { get; }

        public float3 Position { get; }

        public TerminalSpec Spec { get; }

        /// <summary>Ton die nu op het terrein ligt.</summary>
        public float Level { get; internal set; }

        /// <summary>Ton die de poort nu nog kan verwerken (vult aan met de tijd, hoogstens een dag).</summary>
        public float GateBudget { get; internal set; }

        /// <summary>Totaal overgeslagen van schepen en treinen naar de opslag.</summary>
        public float FromCarriers { get; internal set; }

        /// <summary>Totaal overgeslagen van de opslag naar schepen en treinen.</summary>
        public float ToCarriers { get; internal set; }

        /// <summary>Totaal door vrachtwagens aangeleverd.</summary>
        public float FromTrucks { get; internal set; }

        /// <summary>Totaal door vrachtwagens opgehaald.</summary>
        public float ToTrucks { get; internal set; }

        /// <summary>Ton die een vrachtwagen wilde afleveren maar die niet paste of niet door de poort kon.</summary>
        public float Refused { get; internal set; }

        public int DepartedCount { get; internal set; }

        public IReadOnlyList<TerminalCarrier> Carriers => _carriers;

        internal int NextCarrierId => _nextCarrierId;

        public float FreeSpace => math.max(0f, Spec.StorageCapacity - Level);

        public int DockedCount => CountState(CarrierState.Docked);

        public int QueuedCount => CountState(CarrierState.Queued);

        /// <summary>Meldt een schip of trein aan. Het gaat in de rij en legt aan zodra er plek is.</summary>
        public TerminalCarrier Arrive(CarrierMode mode, float capacity, float load, float wanted)
        {
            if (mode != Spec.ServedMode)
            {
                throw new ArgumentException("Dit soort terminal bedient dit voertuig niet.", nameof(mode));
            }

            if (!(capacity > 0f) || !(load >= 0f) || !(wanted >= 0f) || load > capacity || wanted > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Lading en afvoer moeten binnen het laadvermogen vallen.");
            }

            if (_carriers.Count >= MaxCarriers)
            {
                throw new InvalidOperationException("Te veel voertuigen bij deze terminal.");
            }

            var carrier = new TerminalCarrier(_nextCarrierId++, mode, capacity, load, wanted);
            _carriers.Add(carrier);
            return carrier;
        }

        /// <summary>Een vrachtwagen levert af voor verder transport. Geeft de ton die is aangenomen terug.</summary>
        public float ReceiveFromTruck(float tonnes)
        {
            if (!(tonnes >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes));
            }

            var accepted = math.min(tonnes, math.min(GateBudget, FreeSpace));
            GateBudget -= accepted;
            Level += accepted;
            FromTrucks += accepted;
            Refused += tonnes - accepted;
            return accepted;
        }

        /// <summary>Een vrachtwagen haalt op. Geeft de ton die is meegegeven terug.</summary>
        public float LoadTruck(float tonnes)
        {
            if (!(tonnes >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes));
            }

            var given = math.min(tonnes, math.min(GateBudget, Level));
            GateBudget -= given;
            Level -= given;
            ToTrucks += given;
            return given;
        }

        /// <summary>Laat de tijd verstrijken: poortbudget vult aan, voertuigen leggen aan, lossen, laden en vertrekken.</summary>
        public void Step(float days)
        {
            if (!(days >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(days));
            }

            GateBudget = math.min(GateBudget + (Spec.GateRate * days), Spec.GateRate);
            var docked = DockedCount;
            foreach (var carrier in _carriers)
            {
                if (carrier.State == CarrierState.Queued)
                {
                    if (docked >= Spec.Berths)
                    {
                        carrier.WaitedDays += days;
                        continue;
                    }

                    carrier.State = CarrierState.Docked;
                    docked++;
                }

                if (carrier.State == CarrierState.Docked)
                {
                    Work(carrier, days);
                }
            }
        }

        /// <summary>Haalt de vertrokken voertuigen op en verwijdert ze bij de terminal.</summary>
        public List<TerminalCarrier> TakeDeparted()
        {
            var departed = _carriers.FindAll(c => c.State == CarrierState.Departed);
            _carriers.RemoveAll(c => c.State == CarrierState.Departed);
            return departed;
        }

        internal TerminalCarrier RestoreCarrier(int id, CarrierMode mode, float capacity, float load, float wanted)
        {
            var carrier = new TerminalCarrier(id, mode, capacity, load, wanted);
            _carriers.Add(carrier);
            return carrier;
        }

        internal void RestoreNextCarrierId(int next) => _nextCarrierId = next;

        private void Work(TerminalCarrier carrier, float days)
        {
            var budget = Spec.CraneRate * days;
            var unload = math.max(0f, math.min(carrier.ToUnload, math.min(budget, FreeSpace)));
            carrier.Load -= unload;
            carrier.Unloaded += unload;
            Level += unload;
            FromCarriers += unload;
            budget -= unload;

            var room = carrier.Capacity - carrier.Load;
            var load = math.max(0f, math.min(math.min(carrier.Wanted - carrier.Loaded, room), math.min(budget, Level)));
            carrier.Load += load;
            carrier.Loaded += load;
            Level -= load;
            ToCarriers += load;

            carrier.DockedDays += days;
            var finished = carrier.ToUnload <= Epsilon && carrier.Wanted - carrier.Loaded <= Epsilon;
            if (finished || carrier.DockedDays >= Spec.MaxDockDays)
            {
                carrier.State = CarrierState.Departed;
                DepartedCount++;
            }
        }

        private int CountState(CarrierState state)
        {
            var count = 0;
            foreach (var carrier in _carriers)
            {
                if (carrier.State == state)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
