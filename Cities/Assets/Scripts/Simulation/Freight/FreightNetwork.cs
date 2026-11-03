using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Freight
{
    public enum FreightNodeKind : byte
    {
        /// <summary>Een bedrijf dat goederen maakt en klaarzet voor ophalen.</summary>
        Producer = 0,

        /// <summary>Een winkel die goederen verkoopt en dus voorraad nodig heeft.</summary>
        Shop = 1,

        /// <summary>Een buitenverbinding: hier komen import-vrachtwagens vandaan en gaat export naartoe.</summary>
        Outside = 2,
    }

    /// <summary>Een plek waar vrachtwagens laden of lossen (E08-11).</summary>
    public sealed class FreightNode
    {
        internal FreightNode(int id, FreightNodeKind kind, ulong owner, float3 position)
        {
            Id = id;
            Kind = kind;
            Owner = owner;
            Position = position;
        }

        public int Id { get; }

        public FreightNodeKind Kind { get; }

        /// <summary>Het gebouw (verpakt ID) waar dit knooppunt bij hoort; 0 voor een buitenverbinding.</summary>
        public ulong Owner { get; }

        public float3 Position { get; }

        /// <summary>Ton per dag: de productie van een producent of de afzet van een winkel.</summary>
        public float Rate { get; internal set; }

        /// <summary>Ton: de opslag van een producent (uitgaand) of een winkel (voorraad).</summary>
        public float Capacity { get; internal set; }

        /// <summary>Ton die nu klaarstaat (producent) of op voorraad ligt (winkel).</summary>
        public float Level { get; internal set; }

        /// <summary>Ton die al aan een vrachtwagen is toegezegd: nog te leveren (winkel) of nog op te halen (producent).</summary>
        public float Reserved { get; internal set; }

        /// <summary>Ton die een producent kwijtraakte omdat zijn opslag vol was.</summary>
        public float Lost { get; internal set; }

        /// <summary>Ton vraag die een winkel niet kon leveren omdat de voorraad leeg was.</summary>
        public float Unmet { get; internal set; }

        /// <summary>Dagen dat een winkel leeg stond.</summary>
        public float EmptyDays { get; internal set; }

        public bool IsEmpty => Kind == FreightNodeKind.Shop && Level <= 0f;

        /// <summary>Wat een producent nog aan een nieuwe vrachtwagen kan meegeven.</summary>
        public float AvailableToCollect => Math.Max(0f, Level - Reserved);

        /// <summary>Wat een winkel nog kan ontvangen boven voorraad en toezeggingen.</summary>
        public float Room => Math.Max(0f, Capacity - Level - Reserved);
    }

    /// <summary>
    /// De knooppunten van het goederenverkeer (E08-11): producenten zetten hun productie klaar, winkels verbruiken voorraad en buitenverbindingen
    /// leveren en nemen af. De vrachtwagens zelf zitten in <see cref="FreightDispatcher"/>.
    /// </summary>
    public sealed class FreightNetwork
    {
        public const int MaxNodes = 1_000_000;

        private readonly List<FreightNode> _nodes = new List<FreightNode>();

        public IReadOnlyList<FreightNode> Nodes => _nodes;

        public FreightNode AddProducer(ulong owner, float3 position, float tonnesPerDay, float storage)
        {
            CheckAmounts(tonnesPerDay, storage);
            var node = Create(FreightNodeKind.Producer, owner, position);
            node.Rate = tonnesPerDay;
            node.Capacity = storage;
            return node;
        }

        /// <summary>Een winkel begint met een volle voorraad, zodat hij niet meteen leeg staat.</summary>
        public FreightNode AddShop(ulong owner, float3 position, float tonnesPerDay, float stockCapacity)
        {
            CheckAmounts(tonnesPerDay, stockCapacity);
            var node = Create(FreightNodeKind.Shop, owner, position);
            node.Rate = tonnesPerDay;
            node.Capacity = stockCapacity;
            node.Level = stockCapacity;
            return node;
        }

        public FreightNode AddOutsideConnection(float3 position) => Create(FreightNodeKind.Outside, 0, position);

        public FreightNode Get(int id) =>
            id >= 0 && id < _nodes.Count ? _nodes[id] : throw new ArgumentOutOfRangeException(nameof(id), id, "Onbekend knooppunt.");

        /// <summary>Het dichtstbijzijnde knooppunt van een soort dat aan <paramref name="accept"/> voldoet; bij gelijke afstand wint het laagste ID.</summary>
        public FreightNode FindNearest(FreightNodeKind kind, float3 from, Func<FreightNode, bool> accept)
        {
            FreightNode best = null;
            var bestDistance = float.MaxValue;
            foreach (var node in _nodes)
            {
                if (node.Kind != kind || !accept(node))
                {
                    continue;
                }

                var distance = math.distance(node.Position, from);
                if (distance < bestDistance)
                {
                    best = node;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Laat de productie aangroeien en de winkels verkopen over <paramref name="days"/> dagen.</summary>
        public void Step(float days)
        {
            if (!(days >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(days), days, "De tijdstap kan niet negatief zijn.");
            }

            foreach (var node in _nodes)
            {
                if (node.Kind == FreightNodeKind.Producer)
                {
                    Produce(node, days);
                }
                else if (node.Kind == FreightNodeKind.Shop)
                {
                    Sell(node, days);
                }
            }
        }

        internal void Restore(IEnumerable<FreightNode> nodes)
        {
            _nodes.Clear();
            _nodes.AddRange(nodes);
        }

        private static void Produce(FreightNode node, float days)
        {
            var made = node.Rate * days;
            var stored = Math.Max(0f, Math.Min(made, node.Capacity - node.Level));
            node.Level += stored;
            node.Lost += made - stored;
        }

        private static void Sell(FreightNode node, float days)
        {
            var demand = node.Rate * days;
            var sold = Math.Min(demand, node.Level);
            node.Level -= sold;
            if (sold < demand)
            {
                node.Unmet += demand - sold;
                node.EmptyDays += days;
            }
        }

        private static void CheckAmounts(float rate, float capacity)
        {
            if (!(rate >= 0f) || !(capacity >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(rate), "Productie, afzet en opslag kunnen niet negatief zijn.");
            }
        }

        private FreightNode Create(FreightNodeKind kind, ulong owner, float3 position)
        {
            if (_nodes.Count >= MaxNodes)
            {
                throw new InvalidOperationException("Te veel knooppunten voor goederenverkeer.");
            }

            var node = new FreightNode(_nodes.Count, kind, owner, position);
            _nodes.Add(node);
            return node;
        }
    }
}
