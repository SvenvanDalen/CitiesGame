using System;

namespace Cities.Simulation.Problems
{
    /// <summary>Soort probleem waarvoor een gebouw een icoon krijgt. De volgorde is de prioriteit: lager getal is ernstiger.</summary>
    public enum BuildingProblem : byte
    {
        Fire = 0,
        NoPower = 1,
        NoWater = 2,
        GarbageFull = 3,
        NoSewage = 4,
        Crime = 5,
    }

    /// <summary>Verzameling problemen van één gebouw, als bitmasker (geen allocaties).</summary>
    public readonly struct ProblemSet : IEquatable<ProblemSet>
    {
        public const int KindCount = 6;

        private readonly ushort _mask;

        private ProblemSet(ushort mask) => _mask = mask;

        public bool IsEmpty => _mask == 0;

        public int Count
        {
            get
            {
                var n = 0;
                for (var m = (int)_mask; m != 0; m &= m - 1)
                {
                    n++;
                }

                return n;
            }
        }

        /// <summary>Het ernstigste probleem (laagste prioriteitsgetal); alleen geldig als de verzameling niet leeg is.</summary>
        public BuildingProblem Worst
        {
            get
            {
                for (var i = 0; i < KindCount; i++)
                {
                    if ((_mask & (1 << i)) != 0)
                    {
                        return (BuildingProblem)i;
                    }
                }

                throw new InvalidOperationException("De verzameling is leeg.");
            }
        }

        public bool Contains(BuildingProblem problem) => (_mask & (1 << (int)problem)) != 0;

        public ProblemSet With(BuildingProblem problem) => new ProblemSet((ushort)(_mask | (1 << (int)problem)));

        public ProblemSet Without(BuildingProblem problem) => new ProblemSet((ushort)(_mask & ~(1 << (int)problem)));

        /// <summary>Schrijft de problemen op volgorde van ernst in <paramref name="buffer"/>; geeft het aantal geschreven.</summary>
        public int CopyTo(Span<BuildingProblem> buffer)
        {
            var written = 0;
            for (var i = 0; i < KindCount && written < buffer.Length; i++)
            {
                if ((_mask & (1 << i)) != 0)
                {
                    buffer[written++] = (BuildingProblem)i;
                }
            }

            return written;
        }

        public bool Equals(ProblemSet other) => _mask == other._mask;

        public override bool Equals(object obj) => obj is ProblemSet other && Equals(other);

        public override int GetHashCode() => _mask;
    }
}
