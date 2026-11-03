using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Hoe een voertuig getekend wordt (E08-08).</summary>
    public enum VehicleDetail : byte
    {
        /// <summary>Te ver weg: niet getekend, alleen gesimuleerd.</summary>
        Hidden,

        /// <summary>Ver weg: een vereenvoudigd punt of eenvoudig blokje.</summary>
        Point,

        /// <summary>Dichtbij: het volledige model.</summary>
        Full,
    }

    /// <summary>Plaats van een voertuig voor de detailkeuze; ongeldige plekken (lege slots) tellen niet mee.</summary>
    public readonly struct VehiclePlacement
    {
        public VehiclePlacement(float2 position, bool valid = true)
        {
            Position = position;
            Valid = valid;
        }

        public float2 Position { get; }

        public bool Valid { get; }
    }

    /// <summary>
    /// Detailniveau van voertuigen (E08-08). Binnen <see cref="FullRadius"/> van het brandpunt (de camera) krijgt een voertuig het volledige
    /// model, tot hoogstens <see cref="MaxFull"/> stuks (de dichtstbijzijnde winnen); binnen <see cref="PointRadius"/> wordt het een
    /// vereenvoudigd punt; daarbuiten wordt het niet getekend. Een voertuig wisselt pas terug naar een lager niveau als het
    /// <see cref="Margin"/> meter voorbij de grens is (hysterese, zodat er niets flikkert aan de rand). Na de eerste <see cref="Refresh"/>
    /// alloceert het niet meer zolang het aantal voertuigen niet groeit. De simulatie zelf blijft ongemoeid: dit kiest alleen de weergave.
    /// </summary>
    public sealed class VehicleLod
    {
        private static readonly Comparison<Candidate> ByDistance = (a, b) =>
        {
            var c = a.Distance.CompareTo(b.Distance);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        };

        private readonly List<Candidate> _candidates = new List<Candidate>();
        private VehicleDetail[] _detail = Array.Empty<VehicleDetail>();
        private VehicleDetail[] _previous = Array.Empty<VehicleDetail>();

        public VehicleLod(float fullRadius, float pointRadius, float margin, int maxFull)
        {
            FullRadius = math.max(0f, fullRadius);
            PointRadius = math.max(FullRadius, pointRadius);
            Margin = math.max(0f, margin);
            MaxFull = math.max(0, maxFull);
        }

        public float FullRadius { get; }

        public float PointRadius { get; }

        public float Margin { get; }

        public int MaxFull { get; }

        /// <summary>Aantal voertuigen in de laatste verversing.</summary>
        public int Count { get; private set; }

        public int FullCount { get; private set; }

        public int PointCount { get; private set; }

        public int HiddenCount { get; private set; }

        public VehicleDetail DetailOf(int index) =>
            index >= 0 && index < Count ? _detail[index] : VehicleDetail.Hidden;

        public void Refresh(float2 focus, IReadOnlyList<VehiclePlacement> vehicles)
        {
            if (vehicles is null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            var count = vehicles.Count;
            if (_detail.Length < count)
            {
                Array.Resize(ref _detail, count);
                Array.Resize(ref _previous, count);
            }

            (_previous, _detail) = (_detail, _previous);
            Array.Clear(_detail, 0, _detail.Length);
            _candidates.Clear();
            Count = count;
            var valid = 0;
            var points = 0;
            for (var i = 0; i < count; i++)
            {
                var p = vehicles[i];
                if (!p.Valid)
                {
                    continue;
                }

                valid++;
                var distance = math.distance(focus, p.Position);
                var before = _previous[i];
                var fullLimit = before == VehicleDetail.Full ? FullRadius + Margin : FullRadius;
                var pointLimit = before != VehicleDetail.Hidden ? PointRadius + Margin : PointRadius;
                if (distance <= fullLimit)
                {
                    _candidates.Add(new Candidate(distance, i));
                }
                else if (distance <= pointLimit)
                {
                    _detail[i] = VehicleDetail.Point;
                    points++;
                }
            }

            // Dichtstbij eerst; bij een tekort aan volle modellen vallen de verste terug naar een punt.
            _candidates.Sort(ByDistance);
            var full = math.min(MaxFull, _candidates.Count);
            for (var i = 0; i < _candidates.Count; i++)
            {
                _detail[_candidates[i].Index] = i < full ? VehicleDetail.Full : VehicleDetail.Point;
            }

            FullCount = full;
            PointCount = points + _candidates.Count - full;
            HiddenCount = valid - FullCount - PointCount;
        }

        private readonly struct Candidate
        {
            public Candidate(float distance, int index)
            {
                Distance = distance;
                Index = index;
            }

            public float Distance { get; }

            public int Index { get; }
        }
    }

    /// <summary>
    /// Hergebruik van weergave-objecten (E08-08): een vrijgegeven object komt terug uit <see cref="Acquire"/> in plaats van dat er een nieuw
    /// gemaakt wordt, zodat wisselen van detailniveau en voertuigen die komen en gaan geen allocaties geven. Boven <see cref="MaxIdle"/>
    /// wachtende objecten wordt een vrijgegeven object losgelaten.
    /// </summary>
    public sealed class VehicleViewPool<T>
        where T : class
    {
        private readonly Stack<T> _idle = new Stack<T>();
        private readonly Func<T> _create;

        public VehicleViewPool(Func<T> create, int maxIdle)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            MaxIdle = math.max(0, maxIdle);
        }

        public int MaxIdle { get; }

        public int Idle => _idle.Count;

        /// <summary>Aantal objecten dat de pool ooit gemaakt heeft.</summary>
        public int Created { get; private set; }

        public T Acquire()
        {
            if (_idle.Count > 0)
            {
                return _idle.Pop();
            }

            Created++;
            return _create();
        }

        /// <summary>Geeft false als de pool vol zat en het object is losgelaten.</summary>
        public bool Release(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_idle.Count >= MaxIdle)
            {
                return false;
            }

            _idle.Push(item);
            return true;
        }
    }
}
