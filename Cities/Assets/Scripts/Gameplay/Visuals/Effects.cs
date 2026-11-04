using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    public enum ParticleKind : byte
    {
        Smoke,
        Fire,
        Steam,
        Dust,
        Sparks,
    }

    /// <summary>
    /// Deeltjeseffecten met hergebruik (E19-08): rook, vuur, stoom, stof en vonken komen uit een vaste pool per soort, zodat er tijdens het spel
    /// niets gealloceerd wordt. Het budget schaalt met de kwaliteit (<see cref="Density"/>); als de pool vol is, wordt een nieuw effect
    /// geweigerd, behalve dat het oudste effect van dezelfde soort wordt overschreven als <c>overwriteOldest</c> aan staat.
    /// </summary>
    public sealed class ParticlePool
    {
        private static readonly float[] Lifetimes = { 6f, 2.5f, 4f, 3f, 0.8f };
        private static readonly int[] Capacities = { 200, 200, 100, 100, 100 };

        private readonly float[][] _age;
        private readonly float3[][] _position;
        private readonly bool[][] _alive;
        private float _density = 1f;

        public ParticlePool()
        {
            var kinds = Capacities.Length;
            _age = new float[kinds][];
            _position = new float3[kinds][];
            _alive = new bool[kinds][];
            for (var k = 0; k < kinds; k++)
            {
                _age[k] = new float[Capacities[k]];
                _position[k] = new float3[Capacities[k]];
                _alive[k] = new bool[Capacities[k]];
            }
        }

        /// <summary>Aandeel (0 tot 1) van de pool dat gebruikt mag worden; uit het kwaliteitsprofiel.</summary>
        public float Density
        {
            get => _density;
            set => _density = math.saturate(value);
        }

        public static float Lifetime(ParticleKind kind) => Lifetimes[Index(kind)];

        public int Capacity(ParticleKind kind) => (int)(Capacities[Index(kind)] * _density);

        public int ActiveCount(ParticleKind kind)
        {
            var alive = _alive[Index(kind)];
            var count = 0;
            for (var i = 0; i < alive.Length; i++)
            {
                if (alive[i])
                {
                    count++;
                }
            }

            return count;
        }

        public int TotalActive()
        {
            var total = 0;
            for (var k = 0; k < Capacities.Length; k++)
            {
                total += ActiveCount((ParticleKind)k);
            }

            return total;
        }

        /// <summary>Start een effect; geeft de slot terug, of -1 als het budget op is.</summary>
        public int Emit(ParticleKind kind, float3 position, bool overwriteOldest = false)
        {
            var k = Index(kind);
            var limit = Capacity(kind);
            var alive = _alive[k];
            var age = _age[k];
            var free = -1;
            var oldest = -1;
            var count = 0;
            for (var i = 0; i < Capacities[k]; i++)
            {
                if (alive[i])
                {
                    count++;
                    if (oldest < 0 || age[i] > age[oldest])
                    {
                        oldest = i;
                    }
                }
                else if (free < 0)
                {
                    free = i;
                }
            }

            int slot;
            if (count < limit && free >= 0)
            {
                slot = free;
            }
            else if (overwriteOldest && oldest >= 0 && limit > 0)
            {
                slot = oldest;
            }
            else
            {
                return -1;
            }

            alive[slot] = true;
            age[slot] = 0f;
            _position[k][slot] = position;
            return slot;
        }

        public bool IsAlive(ParticleKind kind, int slot) => slot >= 0 && slot < Capacities[Index(kind)] && _alive[Index(kind)][slot];

        public float3 PositionOf(ParticleKind kind, int slot) =>
            IsAlive(kind, slot) ? _position[Index(kind)][slot] : throw new ArgumentException("Dit effect is niet actief.", nameof(slot));

        /// <summary>Levensfase van 0 (net begonnen) tot 1 (bijna weg); voor vervagen en groeien.</summary>
        public float Progress(ParticleKind kind, int slot) =>
            IsAlive(kind, slot) ? math.saturate(_age[Index(kind)][slot] / Lifetimes[Index(kind)]) : throw new ArgumentException("Dit effect is niet actief.", nameof(slot));

        public void Release(ParticleKind kind, int slot)
        {
            if (IsAlive(kind, slot))
            {
                _alive[Index(kind)][slot] = false;
            }
        }

        /// <summary>Laat de tijd lopen; effecten die hun levensduur voorbij zijn gaan terug in de pool.</summary>
        public void Update(float dt)
        {
            if (dt < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijd loopt vooruit.");
            }

            for (var k = 0; k < Capacities.Length; k++)
            {
                var alive = _alive[k];
                var age = _age[k];
                for (var i = 0; i < alive.Length; i++)
                {
                    if (!alive[i])
                    {
                        continue;
                    }

                    age[i] += dt;
                    if (age[i] >= Lifetimes[k])
                    {
                        alive[i] = false;
                    }
                }
            }
        }

        private static int Index(ParticleKind kind)
        {
            var k = (int)kind;
            return k >= 0 && k < Capacities.Length ? k : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekend effect.");
        }
    }

    public enum PropKind : byte
    {
        Grass,
        Bush,
        Rock,
        Tree,
    }

    public readonly struct PropInstance
    {
        public PropInstance(PropKind kind, float2 position, float scale, float rotation)
        {
            Kind = kind;
            Position = position;
            Scale = scale;
            Rotation = rotation;
        }

        public PropKind Kind { get; }

        public float2 Position { get; }

        public float Scale { get; }

        /// <summary>Draaiing om de verticale as in radialen.</summary>
        public float Rotation { get; }
    }

    /// <summary>
    /// Props en begroeiing (E19-09): gras, struiken, rotsen en bomen op een dichtheidskaart, geplaatst op een gejitterd raster. De plaatsing is
    /// deterministisch uit de seed en de cel, dus een gebied ziet er altijd hetzelfde uit en een tegel kan los gevuld worden. Het
    /// kwaliteitsprofiel dunt uit via een vaste drempel per prop: een prop die op laag verdwijnt, komt op hoog altijd terug.
    /// </summary>
    public static class PropScatter
    {
        /// <summary>Afstand tussen rasterpunten in meter.</summary>
        public const float Spacing = 4f;

        public static List<PropInstance> Fill(ulong seed, float2 min, float2 max, Func<float2, float> density, float quality = 1f)
        {
            if (density is null)
            {
                throw new ArgumentNullException(nameof(density));
            }

            if (!(max.x >= min.x) || !(max.y >= min.y))
            {
                throw new ArgumentException("Het gebied heeft een oppervlak.", nameof(max));
            }

            var result = new List<PropInstance>();
            var q = math.saturate(quality);
            var x0 = (int)math.floor(min.x / Spacing);
            var x1 = (int)math.ceil(max.x / Spacing);
            var z0 = (int)math.floor(min.y / Spacing);
            var z1 = (int)math.ceil(max.y / Spacing);
            for (var cz = z0; cz < z1; cz++)
            {
                for (var cx = x0; cx < x1; cx++)
                {
                    var prop = Place(seed, cx, cz, density, q);
                    if (prop.HasValue && prop.Value.Position.x >= min.x && prop.Value.Position.x < max.x
                        && prop.Value.Position.y >= min.y && prop.Value.Position.y < max.y)
                    {
                        result.Add(prop.Value);
                    }
                }
            }

            return result;
        }

        private static PropKind KindFor(float roll)
        {
            if (roll < 0.6f)
            {
                return PropKind.Grass;
            }

            if (roll < 0.85f)
            {
                return PropKind.Bush;
            }

            return roll < 0.95f ? PropKind.Rock : PropKind.Tree;
        }

        private static PropInstance? Place(ulong seed, int cx, int cz, Func<float2, float> density, float quality)
        {
            var random = new DeterministicRandom(SeedHash.Combine(seed, (ulong)(uint)cx, (ulong)(uint)cz));
            var jitter = new float2(random.NextFloat(), random.NextFloat());
            var position = (new float2(cx, cz) + jitter) * Spacing;
            var d = math.saturate(density(position));
            var chance = random.NextFloat();
            var rank = random.NextFloat();
            var kindRoll = random.NextFloat();
            var scale = random.NextFloat(0.8f, 1.3f);
            var rotation = random.NextFloat(0f, 2f * math.PI);

            // Eerst de dichtheidskaart; daarna de kwaliteit met een eigen vaste drempel per prop.
            if (chance >= d || rank >= quality)
            {
                return null;
            }

            return new PropInstance(KindFor(kindRoll), position, scale, rotation);
        }
    }

    /// <summary>Eén draw call: een groep instanties met dezelfde mesh en materiaal, hoogstens <see cref="InstanceBatcher.MaxPerBatch"/>.</summary>
    public readonly struct InstanceBatch
    {
        public InstanceBatch(int key, float4x4[] matrices, int count)
        {
            Key = key;
            Matrices = matrices;
            Count = count;
        }

        /// <summary>Sleutel van mesh en materiaal.</summary>
        public int Key { get; }

        /// <summary>Matrices; alleen de eerste <see cref="Count"/> tellen.</summary>
        public float4x4[] Matrices { get; }

        public int Count { get; }
    }

    /// <summary>
    /// Instancing (E23-06): zet veel gelijke objecten (props, lantaarns, voertuigen) in zo weinig mogelijk draw calls. Instanties worden per
    /// mesh-en-materiaalsleutel gegroepeerd en in batches van hoogstens 1023 verdeeld, de grens van GPU-instancing; dezelfde buffers worden
    /// elk frame hergebruikt.
    /// </summary>
    public sealed class InstanceBatcher
    {
        public const int MaxPerBatch = 1023;

        private readonly Dictionary<int, List<float4x4>> _groups = new Dictionary<int, List<float4x4>>();
        private readonly List<InstanceBatch> _batches = new List<InstanceBatch>();
        private readonly List<float4x4[]> _bufferPool = new List<float4x4[]>();

        public int InstanceCount { get; private set; }

        public void Clear()
        {
            foreach (var list in _groups.Values)
            {
                list.Clear();
            }

            InstanceCount = 0;
        }

        public void Add(int key, float4x4 matrix)
        {
            if (!_groups.TryGetValue(key, out var list))
            {
                list = new List<float4x4>();
                _groups[key] = list;
            }

            list.Add(matrix);
            InstanceCount++;
        }

        /// <summary>Bouwt de batches van wat is toegevoegd; de lijst blijft tot de volgende <see cref="Build"/> geldig.</summary>
        public IReadOnlyList<InstanceBatch> Build()
        {
            _batches.Clear();
            var used = 0;
            foreach (var pair in _groups)
            {
                var list = pair.Value;
                for (var start = 0; start < list.Count; start += MaxPerBatch)
                {
                    var count = math.min(MaxPerBatch, list.Count - start);
                    if (used == _bufferPool.Count)
                    {
                        _bufferPool.Add(new float4x4[MaxPerBatch]);
                    }

                    var buffer = _bufferPool[used++];
                    list.CopyTo(start, buffer, 0, count);
                    _batches.Add(new InstanceBatch(pair.Key, buffer, count));
                }
            }

            return _batches;
        }

        /// <summary>Hoeveel draw calls er nu nodig zijn.</summary>
        public int DrawCalls()
        {
            var calls = 0;
            foreach (var list in _groups.Values)
            {
                calls += (list.Count + MaxPerBatch - 1) / MaxPerBatch;
            }

            return calls;
        }
    }
}
