using System;
using System.Collections.Generic;
using Cities.Simulation.Network;

namespace Cities.Simulation.Pedestrians
{
    /// <summary>Een voetganger of fietser onderweg (E08-12): zijn route over het padennet en hoever hij is.</summary>
    public readonly struct Walker
    {
        public Walker(int id, PathMode mode, IReadOnlyList<int> nodes, int index, float offset, bool onCrossing)
        {
            Id = id;
            Mode = mode;
            Nodes = nodes;
            Index = index;
            Offset = offset;
            OnCrossing = onCrossing;
        }

        public int Id { get; }

        public PathMode Mode { get; }

        /// <summary>De knopen van de route; <see cref="Index"/> is de boog die hij nu loopt (naar <c>Index + 1</c>).</summary>
        public IReadOnlyList<int> Nodes { get; }

        public int Index { get; }

        /// <summary>Afstand in meters op de huidige boog.</summary>
        public float Offset { get; }

        public bool OnCrossing { get; }

        internal Walker With(int index, float offset, bool onCrossing) => new Walker(Id, Mode, Nodes, index, offset, onCrossing);
    }

    /// <summary>
    /// Laat voetgangers en fietsers hun eigen route over het <see cref="PathNetwork"/> lopen. Voor een boog met een oversteek wachten
    /// ze tot <see cref="PedestrianCrossing.MayEnter"/> het toestaat; daarna steken ze in één keer over.
    /// </summary>
    public sealed class WalkerSimulation
    {
        private readonly PathNetwork _network;
        private readonly List<PedestrianCrossing> _crossings = new List<PedestrianCrossing>();
        private readonly List<Walker> _walkers = new List<Walker>();
        private int _nextId = 1;

        public WalkerSimulation(PathNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public IReadOnlyList<Walker> Walkers => _walkers;

        public IReadOnlyList<PedestrianCrossing> Crossings => _crossings;

        public int NextId => _nextId;

        /// <summary>Registreert een oversteek; het nummer is wat <see cref="PathNetwork.AddCrossing"/> verwacht.</summary>
        public int AddCrossing(PedestrianCrossing crossing)
        {
            _crossings.Add(crossing ?? throw new ArgumentNullException(nameof(crossing)));
            return _crossings.Count - 1;
        }

        /// <summary>Zet iemand aan het begin van <paramref name="route"/>; -1 als er niets te lopen valt.</summary>
        public int Add(PathMode mode, PathRoute route)
        {
            if (!route.Found || route.Nodes.Count < 2)
            {
                return -1;
            }

            var id = _nextId++;
            _walkers.Add(new Walker(id, mode, route.Nodes, 0, 0f, false));
            return id;
        }

        /// <param name="dt">Tijdstap in seconden.</param>
        /// <param name="vehicleSeconds">Per oversteek: seconden tot het eerstvolgende voertuig; <c>null</c> betekent geen voertuigen.</param>
        public void Step(float dt, Func<int, float> vehicleSeconds = null)
        {
            if (!(dt > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijdstap moet positief zijn.");
            }

            for (var i = _walkers.Count - 1; i >= 0; i--)
            {
                if (Advance(i, dt, vehicleSeconds))
                {
                    _walkers.RemoveAt(i);
                }
            }
        }

        /// <summary>Of de gelezen toestand past bij het net: bogen bestaan, oversteken bestaan en de afstand klopt.</summary>
        public bool IsValid(Walker walker)
        {
            var nodes = walker.Nodes;
            if (nodes is null || nodes.Count < 2 || walker.Index < 0 || walker.Index >= nodes.Count - 1 || !(walker.Offset >= 0f))
            {
                return false;
            }

            foreach (var node in nodes)
            {
                if (node < 0 || node >= _network.NodeCount)
                {
                    return false;
                }
            }

            if (!_network.TryGetArc(nodes[walker.Index], nodes[walker.Index + 1], walker.Mode, out var length, out var crossing))
            {
                return false;
            }

            return walker.Offset <= length && (!walker.OnCrossing || (crossing >= 0 && crossing < _crossings.Count));
        }

        /// <summary>Vervangt alle wandelaars (laden); de bezetting van de oversteken volgt eruit.</summary>
        internal void Restore(int nextId, IReadOnlyList<Walker> walkers)
        {
            _nextId = nextId;
            _walkers.Clear();
            _walkers.AddRange(walkers);
            foreach (var crossing in _crossings)
            {
                crossing.Occupants = 0;
            }

            foreach (var walker in _walkers)
            {
                if (walker.OnCrossing)
                {
                    _network.TryGetArc(walker.Nodes[walker.Index], walker.Nodes[walker.Index + 1], walker.Mode, out _, out var crossing);
                    _crossings[crossing].Occupants++;
                }
            }
        }

        private bool TryEnter(int crossing, PathMode mode, Func<int, float> vehicleSeconds)
        {
            var gap = vehicleSeconds is null ? float.PositiveInfinity : vehicleSeconds(crossing);
            if (!_crossings[crossing].MayEnter(mode, gap))
            {
                return false;
            }

            _crossings[crossing].Occupants++;
            return true;
        }

        // Geeft true terug als de wandelaar is aangekomen.
        private bool Advance(int slot, float dt, Func<int, float> vehicleSeconds)
        {
            var walker = _walkers[slot];
            var index = walker.Index;
            var offset = walker.Offset;
            var onCrossing = walker.OnCrossing;
            var remaining = dt * (walker.Mode == PathMode.Walk ? PathNetwork.WalkSpeed : PathNetwork.BikeSpeed);
            var arrived = false;
            while (!arrived && remaining > 0f)
            {
                _network.TryGetArc(walker.Nodes[index], walker.Nodes[index + 1], walker.Mode, out var length, out var crossing);
                if (crossing >= 0 && !onCrossing && !TryEnter(crossing, walker.Mode, vehicleSeconds))
                {
                    break;
                }

                onCrossing = crossing >= 0;
                var step = Math.Min(remaining, length - offset);
                offset += step;
                remaining -= step;
                if (offset < length)
                {
                    continue;
                }

                if (onCrossing)
                {
                    _crossings[crossing].Occupants--;
                    onCrossing = false;
                }

                index++;
                offset = 0f;
                arrived = index >= walker.Nodes.Count - 1;
            }

            if (!arrived)
            {
                _walkers[slot] = walker.With(index, offset, onCrossing);
            }

            return arrived;
        }
    }
}
