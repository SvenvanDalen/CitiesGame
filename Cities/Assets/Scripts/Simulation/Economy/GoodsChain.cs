using System;

namespace Cities.Simulation.Economy
{
    /// <summary>Wat een goederenketen in één week aanvoert en vraagt.</summary>
    public readonly struct ChainInput
    {
        public ChainInput(float extracted, float industryCapacity, float shopDemand)
        {
            if (!(extracted >= 0f) || !(industryCapacity >= 0f) || !(shopDemand >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(extracted), "Winning, capaciteit en vraag kunnen niet negatief zijn.");
            }

            Extracted = extracted;
            IndustryCapacity = industryCapacity;
            ShopDemand = shopDemand;
        }

        /// <summary>Grondstof die de winning deze week oplevert.</summary>
        public float Extracted { get; }

        /// <summary>Grondstof die de industrie per week kan verwerken.</summary>
        public float IndustryCapacity { get; }

        /// <summary>Eenheden die de winkels deze week willen verkopen.</summary>
        public float ShopDemand { get; }
    }

    /// <summary>Uitkomst van één week in de keten.</summary>
    public readonly struct ChainWeek
    {
        public ChainWeek(float processed, float delivered, float shortage, float overflow)
        {
            Processed = processed;
            Delivered = delivered;
            Shortage = shortage;
            Overflow = overflow;
        }

        /// <summary>Eenheden die de industrie maakte.</summary>
        public float Processed { get; }

        /// <summary>Eenheden die de winkels kregen.</summary>
        public float Delivered { get; }

        /// <summary>Gevraagde eenheden die niet geleverd konden worden.</summary>
        public float Shortage { get; }

        /// <summary>Grondstof (in eenheden product) die niet meer in de opslag paste en verloren ging; te exporteren door de aanroeper, zie <see cref="Trade"/>.</summary>
        public float Overflow { get; }
    }

    /// <summary>
    /// De goederenketen van één goed (E11-08): winning levert grondstof, industrie verwerkt die tot product en de handel verkoopt het.
    /// Elke schakel heeft een voorraad met een maximum. Een tekort in een schakel loopt door naar de volgende: te weinig grondstof geeft
    /// te weinig product, en te weinig product geeft lege schappen. De winkels kunnen dan maar een deel van hun vraag verkopen
    /// (<see cref="Availability"/>), dus <see cref="Company.Step"/> levert minder omzet en belasting.
    /// </summary>
    public sealed class GoodsChain
    {
        /// <summary>Onder dit aandeel geleverde vraag spreken we van lege schappen.</summary>
        public const float EmptyShelfThreshold = 0.5f;

        private readonly float _rawPerUnit;
        private readonly float _rawStorage;
        private readonly float _productStorage;

        public GoodsChain(string name, float rawPerUnit, float rawStorage, float productStorage)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een goed heeft een naam nodig.", nameof(name));
            }

            if (!(rawPerUnit > 0f) || !(rawStorage >= 0f) || !(productStorage >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(rawPerUnit), "De grondstof per eenheid is positief en de opslag niet negatief.");
            }

            Name = name;
            _rawPerUnit = rawPerUnit;
            _rawStorage = rawStorage;
            _productStorage = productStorage;
            Availability = 1f;
        }

        public string Name { get; }

        public float RawStock { get; private set; }

        public float ProductStock { get; private set; }

        /// <summary>Aandeel van de vraag dat vorige week geleverd werd (0 tot 1); 1 zolang er nog niets gevraagd is.</summary>
        public float Availability { get; private set; }

        public bool HasEmptyShelves => Availability < EmptyShelfThreshold;

        /// <summary>De vraag die de winkels effectief kunnen bedienen, voor <see cref="Company.Step"/>.</summary>
        public float EffectiveDemand(float demand) => Math.Max(0f, demand) * Availability;

        public ChainWeek Step(in ChainInput input)
        {
            var raw = RawStock + input.Extracted;
            var rawOverflow = Math.Max(0f, raw - _rawStorage);
            raw -= rawOverflow;

            var made = Math.Min(Math.Min(raw, input.IndustryCapacity) / _rawPerUnit, Math.Max(0f, _productStorage - ProductStock));
            raw -= made * _rawPerUnit;
            var product = ProductStock + made;

            var delivered = Math.Min(product, input.ShopDemand);
            product -= delivered;
            var shortage = input.ShopDemand - delivered;

            RawStock = raw;
            ProductStock = product;
            Availability = input.ShopDemand > 0f ? delivered / input.ShopDemand : 1f;
            return new ChainWeek(made, delivered, shortage, rawOverflow / _rawPerUnit);
        }

        internal void Restore(float rawStock, float productStock, float availability)
        {
            RawStock = Math.Min(rawStock, _rawStorage);
            ProductStock = Math.Min(productStock, _productStorage);
            Availability = Math.Max(0f, Math.Min(1f, availability));
        }
    }
}
