using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Alle batterijen van de stad (E06-13). Elke batterij heeft een vermogen (MW), een opslag (MWh) en een rendement; laden en
    /// ontladen verliest energie. Het bewaarde aantal MWh is de status en gaat in sectie <c>BATT</c>.
    /// </summary>
    public sealed class PowerStorage : ISaveSection
    {
        public const string SectionId = "BATT";

        private const int MaxBatteries = 100_000;

        private readonly List<Battery> _batteries = new List<Battery>();

        public string Id => SectionId;

        public int Version => 1;

        public int Count => _batteries.Count;

        public float TotalStored
        {
            get
            {
                var total = 0f;
                foreach (var b in _batteries)
                {
                    total += b.Stored;
                }

                return total;
            }
        }

        public int Add(float powerMw, float capacityMwh, float roundTripEfficiency)
        {
            if (!(powerMw > 0f) || !(capacityMwh > 0f) || !(roundTripEfficiency > 0f && roundTripEfficiency <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(powerMw), "Een batterij heeft vermogen, opslag en een rendement tussen 0 en 1 nodig.");
            }

            _batteries.Add(new Battery(powerMw, capacityMwh, math.sqrt(roundTripEfficiency), 0f));
            return _batteries.Count - 1;
        }

        public float Stored(int id) => _batteries[id].Stored;

        /// <summary>
        /// Zet het netto overschot (positief) of tekort (negatief) van het net in megawatt om in laden en ontladen voor
        /// <paramref name="hours"/> uur. Geeft het nieuwe saldo terug: wat de batterijen niet konden opnemen of leveren blijft over.
        /// </summary>
        public float Balance(float netMw, float hours)
        {
            if (!(hours > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(hours), "De tijdstap moet positief zijn.");
            }

            foreach (var b in _batteries)
            {
                if (netMw > 0f)
                {
                    var intake = math.min(math.min(netMw, b.PowerMw), (b.CapacityMwh - b.Stored) / (hours * b.OneWayEfficiency));
                    b.Stored += intake * hours * b.OneWayEfficiency;
                    netMw -= intake;
                }
                else if (netMw < 0f)
                {
                    var delivery = math.min(math.min(-netMw, b.PowerMw), b.Stored * b.OneWayEfficiency / hours);
                    b.Stored -= delivery * hours / b.OneWayEfficiency;
                    netMw += delivery;
                }
            }

            return netMw;
        }

        public void Write(SaveWriter writer)
        {
            writer.Write(_batteries.Count);
            foreach (var b in _batteries)
            {
                writer.Write(b.PowerMw);
                writer.Write(b.CapacityMwh);
                writer.Write(b.OneWayEfficiency);
                writer.Write(b.Stored);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxBatteries)
            {
                throw reader.Corrupt($"onmogelijk aantal batterijen {count}");
            }

            var batteries = new List<Battery>(count);
            for (var i = 0; i < count; i++)
            {
                var b = new Battery(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                if (!(b.PowerMw > 0f) || !(b.CapacityMwh > 0f) || !(b.OneWayEfficiency > 0f && b.OneWayEfficiency <= 1f) || !(b.Stored >= 0f && b.Stored <= b.CapacityMwh))
                {
                    throw reader.Corrupt($"ongeldige batterij {i}");
                }

                batteries.Add(b);
            }

            reader.ExpectEnd();
            return () =>
            {
                _batteries.Clear();
                _batteries.AddRange(batteries);
            };
        }

        private sealed class Battery
        {
            public Battery(float powerMw, float capacityMwh, float oneWayEfficiency, float stored)
            {
                PowerMw = powerMw;
                CapacityMwh = capacityMwh;
                OneWayEfficiency = oneWayEfficiency;
                Stored = stored;
            }

            public float PowerMw { get; }

            public float CapacityMwh { get; }

            public float OneWayEfficiency { get; }

            public float Stored { get; set; }
        }
    }
}
