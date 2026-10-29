using System;
using Cities.Core.Persistence;
using Unity.Collections;

namespace Cities.Core.Ids
{
    /// <summary>
    /// Deelt <see cref="EntityId{TKind}"/>'s uit en houdt per plek de huidige generatie bij.
    /// Vrijgekomen plekken worden in volgorde van vrijgeven hergebruikt (FIFO), zodat een plek zo lang mogelijk
    /// ongebruikt blijft en oude verwijzingen niet snel opnieuw geldig lijken.
    /// </summary>
    /// <remarks>
    /// Opslag in Collections-containers, zodat de simulatie de levenstatus ook in Burst-jobs kan lezen
    /// (<see cref="IsAlive"/>, zolang er in die tijd niet wordt uitgedeeld of vrijgegeven).
    /// Een plek waarvan de generatie de maximale waarde bereikt, wordt niet meer hergebruikt.
    /// </remarks>
    public struct IdAllocator<TKind> : IDisposable where TKind : struct, IEntityKind
    {
        private NativeList<uint> _generations;
        private NativeQueue<int> _free;
        private int _aliveCount;
        private int _retiredCount;

        public IdAllocator(int initialCapacity, Allocator allocator)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Capaciteit kan niet negatief zijn.");
            }

            _generations = new NativeList<uint>(initialCapacity, allocator);
            _free = new NativeQueue<int>(allocator);
            _aliveCount = 0;
            _retiredCount = 0;
        }

        public bool IsCreated => _generations.IsCreated;

        /// <summary>Aantal levende ID's.</summary>
        public int AliveCount => _aliveCount;

        /// <summary>Aantal plekken dat ooit is uitgedeeld; de hoogste index is <c>SlotCount - 1</c>.</summary>
        public int SlotCount => _generations.Length;

        /// <summary>Plekken die door een uitgeputte generatie niet meer worden hergebruikt.</summary>
        public int RetiredCount => _retiredCount;

        public EntityId<TKind> Allocate()
        {
            if (_free.TryDequeue(out var index))
            {
                var generation = _generations[index] + 1;
                _generations[index] = generation;
                _aliveCount++;
                return new EntityId<TKind>(index, generation);
            }

            if (_generations.Length == int.MaxValue)
            {
                throw new InvalidOperationException($"Geen ID's meer beschikbaar voor {typeof(TKind).Name}.");
            }

            _generations.Add(1u);
            _aliveCount++;
            return new EntityId<TKind>(_generations.Length - 1, 1u);
        }

        /// <summary>Geeft een ID vrij. Geeft false bij een ID dat niet (meer) leeft, zonder iets te wijzigen.</summary>
        public bool Release(EntityId<TKind> id)
        {
            if (!IsAlive(id))
            {
                return false;
            }

            // Een even generatie betekent "vrij": oneven is levend. Zo is aan de generatie zelf te zien of de plek bezet is.
            var freed = id.Generation + 1;
            _generations[id.Index] = freed;
            _aliveCount--;
            if (freed >= uint.MaxValue - 1)
            {
                _retiredCount++;
            }
            else
            {
                _free.Enqueue(id.Index);
            }

            return true;
        }

        public readonly bool IsAlive(EntityId<TKind> id) =>
            id.IsValid && id.Index < _generations.Length && _generations[id.Index] == id.Generation && (id.Generation & 1u) == 1u;

        /// <summary>De levende ID op een plek, of <see cref="EntityId{TKind}.None"/> als de plek vrij is.</summary>
        public readonly EntityId<TKind> GetAliveAt(int index)
        {
            if (index < 0 || index >= _generations.Length)
            {
                return EntityId<TKind>.None;
            }

            var generation = _generations[index];
            return (generation & 1u) == 1u ? new EntityId<TKind>(index, generation) : EntityId<TKind>.None;
        }

        /// <summary>
        /// Schrijft generaties en vrije plekken (in volgorde), zodat na laden dezelfde ID's in dezelfde volgorde
        /// worden uitgedeeld als zonder opslaan.
        /// </summary>
        public readonly void WriteTo(SaveWriter writer)
        {
            writer.WriteArray<uint>(_generations.AsArray().AsReadOnlySpan());
            using (var free = _free.ToArray(Allocator.Temp))
            {
                writer.WriteArray<int>(free.AsReadOnlySpan());
            }
        }

        /// <summary>Vervangt de inhoud door een staat uit <see cref="IdAllocatorState.Read"/>.</summary>
        public void Restore(IdAllocatorState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _generations.Clear();
            _generations.ResizeUninitialized(state.Generations.Length);
            _generations.AsArray().CopyFrom(state.Generations);
            _free.Clear();
            foreach (var index in state.Free)
            {
                _free.Enqueue(index);
            }

            _aliveCount = state.AliveCount;
            _retiredCount = state.RetiredCount;
        }

        public void Dispose()
        {
            if (_generations.IsCreated)
            {
                _generations.Dispose();
            }

            if (_free.IsCreated)
            {
                _free.Dispose();
            }
        }
    }
}
