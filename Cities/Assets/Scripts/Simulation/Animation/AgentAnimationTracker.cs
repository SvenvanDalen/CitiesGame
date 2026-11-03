using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Animation
{
    /// <summary>Wat er per agent animeert.</summary>
    public enum AgentAnimationKind : byte
    {
        /// <summary>Wielhoek in radialen (voertuigen).</summary>
        Wheel,

        /// <summary>Looppas 0..1 van een gebakken clip (burgers).</summary>
        Walk,
    }

    /// <summary>
    /// Houdt per agent de animatiestand bij (E21-05) uit zijn verplaatsing sinds de vorige verversing, zodat de weergave alleen posities
    /// hoeft aan te leveren. Een nieuwe agent begint zonder sprong: een voertuig met wielhoek 0, een burger met een eigen startfase uit
    /// <see cref="AnimationPhase"/>. Agents die een verversing niet meer voorkomen worden vergeten, dus de tabel groeit niet door.
    /// De stand is afgeleid en zit daarom niet in de save.
    /// </summary>
    public sealed class AgentAnimationTracker
    {
        private readonly AgentAnimationKind _kind;
        private readonly float _size;
        private readonly Dictionary<uint, State> _states = new Dictionary<uint, State>();
        private readonly List<uint> _gone = new List<uint>();
        private int _generation;

        /// <param name="kind">Wiel of looppas.</param>
        /// <param name="size">Wielstraal in meters voor <see cref="AgentAnimationKind.Wheel"/>, schaal van de burger (1 = volwassene) voor <see cref="AgentAnimationKind.Walk"/>.</param>
        public AgentAnimationTracker(AgentAnimationKind kind, float size)
        {
            if (size <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "De grootte moet positief zijn.");
            }

            _kind = kind;
            _size = size;
        }

        /// <summary>Aantal agents dat nu bijgehouden wordt.</summary>
        public int Count => _states.Count;

        /// <summary>Begin een verversing; roep daarna <see cref="Update"/> per zichtbare agent aan en sluit af met <see cref="End"/>.</summary>
        public void Begin() => _generation++;

        /// <summary>Werkt de stand van een agent bij en geeft de wielhoek of looppas terug.</summary>
        /// <param name="forward">Richting waarin de agent kijkt; achteruit rijden draait de wielen terug.</param>
        public float Update(uint id, float2 position, float2 forward)
        {
            if (!_states.TryGetValue(id, out var state))
            {
                state = new State(position, _kind == AgentAnimationKind.Walk ? AnimationPhase.Offset(id) : 0f, _generation);
                _states[id] = state;
                return state.Value;
            }

            var delta = position - state.Position;
            var distance = math.length(delta);
            if (distance > 0f && math.lengthsq(forward) > 0f && math.dot(delta, forward) < 0f && _kind == AgentAnimationKind.Wheel)
            {
                distance = -distance;
            }

            var value = _kind == AgentAnimationKind.Wheel
                ? WheelSpin.Advance(state.Value, distance, _size)
                : WalkCycle.Advance(state.Value, math.abs(distance), _size);
            _states[id] = new State(position, value, _generation);
            return value;
        }

        /// <summary>Vergeet agents die sinds <see cref="Begin"/> niet meer zijn bijgewerkt (verdwenen, buiten bereik of naar een lager detailniveau).</summary>
        public int End()
        {
            _gone.Clear();
            foreach (var pair in _states)
            {
                if (pair.Value.Generation != _generation)
                {
                    _gone.Add(pair.Key);
                }
            }

            foreach (var id in _gone)
            {
                _states.Remove(id);
            }

            return _gone.Count;
        }

        private readonly struct State
        {
            public State(float2 position, float value, int generation)
            {
                Position = position;
                Value = value;
                Generation = generation;
            }

            public float2 Position { get; }

            public float Value { get; }

            public int Generation { get; }
        }
    }
}
