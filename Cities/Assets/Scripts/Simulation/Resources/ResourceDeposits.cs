using System;
using System.Collections.Generic;

namespace Cities.Simulation.Resources
{
    /// <summary>Wat er in één cel van de resource-laag ligt.</summary>
    public sealed class Deposit
    {
        internal Deposit(ResourceKind kind, float capacity, float remaining)
        {
            Kind = kind;
            Capacity = capacity;
            Remaining = remaining;
        }

        public ResourceKind Kind { get; }

        /// <summary>De oorspronkelijke hoeveelheid (bij gewassen de vruchtbaarheid per week).</summary>
        public float Capacity { get; }

        public float Remaining { get; internal set; }

        public bool IsDepleted => Remaining <= 0f;
    }

    /// <summary>De resource-laag: per cel een afzetting van een soort (E14-01). Spaarzaam opgeslagen en op celvolgorde doorlopen voor determinisme.</summary>
    public sealed class ResourceDeposits
    {
        public const int MaxDeposits = 4_000_000;

        private readonly SortedDictionary<long, Deposit> _deposits = new SortedDictionary<long, Deposit>();

        public int Count => _deposits.Count;

        public IEnumerable<KeyValuePair<long, Deposit>> All => _deposits;

        public static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        public static int KeyX(long key) => (int)(key >> 32);

        public static int KeyZ(long key) => (int)(key & 0xFFFFFFFF);

        public Deposit Set(int x, int z, ResourceKind kind, float capacity)
        {
            if (!ResourceKinds.IsValid(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Een afzetting heeft een positieve hoeveelheid.");
            }

            var key = Key(x, z);
            if (!_deposits.ContainsKey(key) && _deposits.Count >= MaxDeposits)
            {
                throw new InvalidOperationException("Te veel afzettingen.");
            }

            var deposit = new Deposit(kind, capacity, capacity);
            _deposits[key] = deposit;
            return deposit;
        }

        public bool Remove(int x, int z) => _deposits.Remove(Key(x, z));

        public Deposit Get(int x, int z) => _deposits.TryGetValue(Key(x, z), out var d) ? d : null;

        internal void Restore(long key, Deposit deposit) => _deposits[key] = deposit;

        internal void Clear() => _deposits.Clear();

        internal static Deposit Create(ResourceKind kind, float capacity, float remaining) => new Deposit(kind, capacity, remaining);
    }
}
