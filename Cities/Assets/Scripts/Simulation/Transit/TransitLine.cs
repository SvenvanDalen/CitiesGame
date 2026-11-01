using System;
using System.Collections.Generic;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// Eén openbaarvervoerlijn (E09-01): een geordende rij haltes, heen en terug of als ronde, met het aantal voertuigen en de
    /// gewenste tijd tussen twee voertuigen (<see cref="Headway"/>, seconden). Onveranderlijk; bewerken gaat via <see cref="TransitNetwork.Update"/>.
    /// </summary>
    public sealed class TransitLine
    {
        public const int MaxStops = 500;
        public const int MaxVehicles = 1000;

        private readonly int[] _stops;

        public TransitLine(int id, string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een lijn heeft een naam nodig.", nameof(name));
            }

            if (stops == null || stops.Count < 2 || stops.Count > MaxStops)
            {
                throw new ArgumentException($"Een lijn heeft 2 tot {MaxStops} haltes nodig.", nameof(stops));
            }

            for (var i = 1; i < stops.Count; i++)
            {
                if (stops[i] == stops[i - 1])
                {
                    throw new ArgumentException("Twee opeenvolgende haltes zijn dezelfde.", nameof(stops));
                }
            }

            if (isLoop && stops[0] == stops[stops.Count - 1])
            {
                throw new ArgumentException("Een ronde begint en eindigt niet op dezelfde halte.", nameof(stops));
            }

            if (vehicles < 1 || vehicles > MaxVehicles)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicles), vehicles, $"Een lijn heeft 1 tot {MaxVehicles} voertuigen.");
            }

            if (!(headway > 0f) || float.IsInfinity(headway))
            {
                throw new ArgumentOutOfRangeException(nameof(headway), headway, "De tijd tussen voertuigen moet groter dan nul zijn.");
            }

            Id = id;
            Name = name;
            _stops = new int[stops.Count];
            for (var i = 0; i < _stops.Length; i++)
            {
                _stops[i] = stops[i];
            }

            IsLoop = isLoop;
            Vehicles = vehicles;
            Headway = headway;
        }

        public int Id { get; }

        public string Name { get; }

        public IReadOnlyList<int> Stops => _stops;

        /// <summary>Of de lijn een ronde is (laatste halte terug naar de eerste) in plaats van heen en terug.</summary>
        public bool IsLoop { get; }

        public int Vehicles { get; }

        /// <summary>Gewenste tijd tussen twee voertuigen in seconden.</summary>
        public float Headway { get; }
    }
}
