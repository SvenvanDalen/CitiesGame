using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Resources
{
    /// <summary>Een winningsgebouw (mijn, boorplatform, houtzagerij, boerderij) op één of meer cellen van de resource-laag.</summary>
    public sealed class Extractor
    {
        internal Extractor(int id, ResourceKind kind, (int X, int Z)[] cells, float ratePerCell, bool depletes)
        {
            Id = id;
            Kind = kind;
            Cells = cells;
            RatePerCell = ratePerCell;
            Depletes = depletes;
        }

        public int Id { get; }

        public ResourceKind Kind { get; }

        public IReadOnlyList<(int X, int Z)> Cells { get; }

        /// <summary>Maximale winning per cel per week.</summary>
        public float RatePerCell { get; }

        /// <summary>Of de afzetting door dit gebouw opraakt (optioneel; uit is een onuitputtelijke bron).</summary>
        public bool Depletes { get; }

        /// <summary>Opbrengst van vorige week.</summary>
        public float LastYield { get; internal set; }
    }

    /// <summary>
    /// Winning op de resource-laag (E14-01). Een gebouw wint per week uit de cellen onder zijn footprint die van zijn soort zijn.
    /// Olie en erts raken bij uitputting op; hout groeit terug; gewassen leveren tegen de vruchtbaarheid van de cel maal een seizoensfactor en raken niet op.
    /// De opbrengst per soort is de <c>Extracted</c> van <c>GoodsChain.Step</c> voor het goed uit <see cref="ResourceKinds.Good"/>.
    /// </summary>
    public sealed class ResourceExtraction
    {
        public const int MaxExtractors = 100_000;
        public const int MaxCells = 1024;

        private readonly SortedDictionary<int, Extractor> _extractors = new SortedDictionary<int, Extractor>();
        private readonly float[] _lastWeek = new float[ResourceKinds.Count];

        public ResourceExtraction(ResourceDeposits deposits)
        {
            Deposits = deposits ?? throw new ArgumentNullException(nameof(deposits));
            NextId = 1;
        }

        public ResourceDeposits Deposits { get; }

        public int NextId { get; internal set; }

        public IEnumerable<Extractor> Extractors => _extractors.Values;

        public Extractor Get(int id) => _extractors.TryGetValue(id, out var e) ? e : null;

        /// <summary>Of er onder de footprint minstens één cel van deze soort ligt; een gebouw mag alleen daar staan.</summary>
        public bool CanPlace(ResourceKind kind, IEnumerable<(int X, int Z)> cells) =>
            cells != null && cells.Any(c => Deposits.Get(c.X, c.Z)?.Kind == kind);

        public Extractor Add(ResourceKind kind, IEnumerable<(int X, int Z)> cells, float ratePerCell, bool depletes)
        {
            var array = cells?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(cells));
            if (!ResourceKinds.IsValid(kind) || !(ratePerCell > 0f) || array.Length == 0 || array.Length > MaxCells)
            {
                throw new ArgumentException("Een winningsgebouw heeft een geldige soort, een positieve snelheid en 1 tot " + MaxCells + " cellen.");
            }

            if (!CanPlace(kind, array))
            {
                throw new InvalidOperationException("Onder het gebouw ligt geen " + ResourceKinds.Good(kind) + ".");
            }

            if (_extractors.Count >= MaxExtractors)
            {
                throw new InvalidOperationException("Te veel winningsgebouwen.");
            }

            var extractor = new Extractor(NextId++, kind, array, ratePerCell, depletes);
            _extractors.Add(extractor.Id, extractor);
            return extractor;
        }

        public bool Remove(int id) => _extractors.Remove(id);

        /// <summary>Opbrengst van de laatste <see cref="Step"/> voor een soort.</summary>
        public float LastWeek(ResourceKind kind) => ResourceKinds.IsValid(kind) ? _lastWeek[(int)kind] : 0f;

        /// <summary>
        /// Eén week winning. <paramref name="growingSeason"/> (0 tot 1) schaalt alleen gewassen. Daarna groeit hout terug op afzettingen die al bewerkt zijn.
        /// </summary>
        public void Step(float growingSeason)
        {
            if (!(growingSeason >= 0f && growingSeason <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(growingSeason), "De groeifactor ligt tussen 0 en 1.");
            }

            Array.Clear(_lastWeek, 0, _lastWeek.Length);
            foreach (var extractor in _extractors.Values)
            {
                var yield = 0f;
                foreach (var (x, z) in extractor.Cells)
                {
                    yield += Harvest(extractor, Deposits.Get(x, z), growingSeason);
                }

                extractor.LastYield = yield;
                _lastWeek[(int)extractor.Kind] += yield;
            }

            Regrow();
        }

        private static float Harvest(Extractor extractor, Deposit deposit, float growingSeason)
        {
            if (deposit == null || deposit.Kind != extractor.Kind)
            {
                return 0f;
            }

            if (deposit.Kind == ResourceKind.Crops)
            {
                return Math.Min(extractor.RatePerCell, deposit.Capacity) * growingSeason;
            }

            var taken = Math.Min(extractor.RatePerCell, deposit.Remaining);
            if (extractor.Depletes)
            {
                deposit.Remaining -= taken;
            }

            return taken;
        }

        private void Regrow()
        {
            foreach (var pair in Deposits.All)
            {
                var d = pair.Value;
                if (d.Kind == ResourceKind.Wood && d.Remaining < d.Capacity)
                {
                    d.Remaining = Math.Min(d.Capacity, d.Remaining + d.Capacity * ResourceKinds.WoodRegrowthPerWeek);
                }
            }
        }

        internal void Restore(IEnumerable<Extractor> extractors, int nextId)
        {
            _extractors.Clear();
            foreach (var e in extractors)
            {
                _extractors.Add(e.Id, e);
            }

            NextId = nextId;
        }

        internal static Extractor Create(int id, ResourceKind kind, (int X, int Z)[] cells, float rate, bool depletes) =>
            new Extractor(id, kind, cells, rate, depletes);
    }
}
