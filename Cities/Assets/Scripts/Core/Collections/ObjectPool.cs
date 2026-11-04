using System;
using System.Collections.Generic;

namespace Cities.Core.Collections
{
    /// <summary>
    /// Eenvoudige pool voor managed objecten die vaak nodig zijn en dan weer weg mogen (E23-08): buffers, lijsten en
    /// tijdelijke werkobjecten. Houdt hooguit <see cref="MaxRetained"/> objecten vast; wat daarboven wordt teruggegeven
    /// vervalt aan de GC. Niet thread-safe: gebruik één pool per thread of beschermd door de aanroeper.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _free = new Stack<T>();
        private readonly Func<T> _create;
        private readonly Action<T> _reset;

        /// <param name="create">Maakt een nieuw object als de pool leeg is.</param>
        /// <param name="reset">Maakt een object schoon bij teruggeven (bijvoorbeeld <c>list.Clear()</c>).</param>
        /// <param name="maxRetained">Hoeveel vrije objecten de pool bewaart.</param>
        public ObjectPool(Func<T> create, Action<T> reset = null, int maxRetained = 64)
        {
            if (maxRetained < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetained), "Kan niet negatief zijn.");
            }

            _create = create ?? throw new ArgumentNullException(nameof(create));
            _reset = reset;
            MaxRetained = maxRetained;
        }

        public int MaxRetained { get; }

        /// <summary>Vrije objecten die klaarstaan.</summary>
        public int FreeCount => _free.Count;

        /// <summary>Hoeveel objecten de pool in totaal nieuw moest maken; een stabiele waarde betekent dat pooling werkt.</summary>
        public int CreatedCount { get; private set; }

        /// <summary>Objecten die nu uitgeleend zijn.</summary>
        public int InUse { get; private set; }

        public T Get()
        {
            InUse++;
            if (_free.Count > 0)
            {
                return _free.Pop();
            }

            CreatedCount++;
            return _create();
        }

        public void Return(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (InUse > 0)
            {
                InUse--;
            }

            _reset?.Invoke(item);
            if (_free.Count < MaxRetained)
            {
                _free.Push(item);
            }
        }

        /// <summary>Leent een object uit voor een <c>using</c>-blok.</summary>
        public Lease Rent() => new Lease(this, Get());

        public void Clear() => _free.Clear();

        public readonly struct Lease : IDisposable
        {
            private readonly ObjectPool<T> _pool;

            internal Lease(ObjectPool<T> pool, T value)
            {
                _pool = pool;
                Value = value;
            }

            public T Value { get; }

            public void Dispose() => _pool.Return(Value);
        }
    }

    /// <summary>Gedeelde pools voor veelgebruikte tijdelijke lijsten.</summary>
    public static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> Pool = new ObjectPool<List<T>>(() => new List<T>(), list => list.Clear(), 32);

        public static ObjectPool<List<T>> Instance => Pool;

        public static List<T> Get() => Pool.Get();

        public static void Return(List<T> list) => Pool.Return(list);
    }
}
