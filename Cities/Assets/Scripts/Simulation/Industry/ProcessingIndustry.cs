using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;

namespace Cities.Simulation.Industry
{
    /// <summary>Wat een winkel van een product wil ontvangen.</summary>
    public readonly struct ShopRequest
    {
        public ShopRequest(int shopId, float amount)
        {
            ShopId = shopId;
            Amount = Math.Max(0f, amount);
        }

        public int ShopId { get; }

        /// <summary>Gewenste ton.</summary>
        public float Amount { get; }
    }

    /// <summary>Wat een winkel van een levering kreeg.</summary>
    public readonly struct ShopDelivery
    {
        public ShopDelivery(int shopId, float requested, float delivered)
        {
            ShopId = shopId;
            Requested = requested;
            Delivered = delivered;
        }

        public int ShopId { get; }

        public float Requested { get; }

        public float Delivered { get; }

        public float Shortage => Requested - Delivered;
    }

    /// <summary>
    /// De verwerkende industrie van de stad (E14-03), bovenop de goederenketen van E11-08: grondstof komt binnen via <see cref="SupplyRaw"/>,
    /// de fabrieken maken er eindproducten van en <see cref="DeliverToShops"/> verdeelt die over de winkels. Bij schaarste krijgt elke winkel
    /// hetzelfde aandeel van zijn vraag, zodat geen winkel stelselmatig achteraan staat.
    /// </summary>
    public sealed class ProcessingIndustry
    {
        public const int MaxPlants = 100_000;

        private readonly SortedDictionary<int, ProcessingPlant> _plants = new SortedDictionary<int, ProcessingPlant>();

        public ProcessingIndustry(RecipeBook recipes)
        {
            Recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            NextId = 1;
        }

        public RecipeBook Recipes { get; }

        public int NextId { get; private set; }

        public IEnumerable<ProcessingPlant> Plants => _plants.Values;

        public int Count => _plants.Count;

        public ProcessingPlant Add(string product, float efficiency = 1f)
        {
            if (_plants.Count >= MaxPlants)
            {
                throw new InvalidOperationException("Te veel fabrieken.");
            }

            var plant = new ProcessingPlant(NextId, Recipes.Get(product), efficiency);
            _plants.Add(NextId, plant);
            NextId++;
            return plant;
        }

        public bool Remove(int id) => _plants.Remove(id);

        public ProcessingPlant Get(int id) =>
            _plants.TryGetValue(id, out var plant) ? plant : throw new KeyNotFoundException("Onbekende fabriek: " + id);

        /// <summary>Totaal klaarstaand eindproduct, in ton.</summary>
        public float Stock(string product) => _plants.Values.Where(p => p.Recipe.Product == product).Sum(p => p.OutputStock);

        /// <summary>Grondstof (ton per dag) die alle fabrieken van een product bij hun bezetting aankunnen.</summary>
        public float RawCapacityPerDay(string product) => _plants.Values.Where(p => p.Recipe.Product == product).Sum(p => p.RawCapacityPerDay);

        /// <summary>
        /// Verdeelt grondstof over de fabrieken die hem gebruiken, naar rato van hun vrije opslag.
        /// Geeft terug hoeveel er niet in paste (te exporteren of te laten liggen).
        /// </summary>
        public float SupplyRaw(string resource, float amount)
        {
            if (!(amount > 0f))
            {
                return 0f;
            }

            var users = _plants.Values.Where(p => p.InputRoom(resource) > 0f).ToArray();
            var totalRoom = users.Sum(p => p.InputRoom(resource));
            if (totalRoom <= 0f)
            {
                return amount;
            }

            var share = Math.Min(1f, amount / totalRoom);
            var left = amount;
            foreach (var plant in users)
            {
                left -= plant.Accept(resource, plant.InputRoom(resource) * share);
            }

            return Math.Max(0f, left);
        }

        /// <summary>Laat alle fabrieken draaien en geeft per product wat er gemaakt is.</summary>
        public IReadOnlyDictionary<string, float> Step(float days)
        {
            var made = new SortedDictionary<string, float>(StringComparer.Ordinal);
            foreach (var plant in _plants.Values)
            {
                var produced = plant.Step(days);
                made[plant.Recipe.Product] = (made.TryGetValue(plant.Recipe.Product, out var sum) ? sum : 0f) + produced;
            }

            return made;
        }

        /// <summary>
        /// Levert product aan de winkels. Is er te weinig, dan krijgt elke winkel hetzelfde aandeel van zijn vraag.
        /// De volgorde van het resultaat is die van <paramref name="requests"/>.
        /// </summary>
        public IReadOnlyList<ShopDelivery> DeliverToShops(string product, IReadOnlyList<ShopRequest> requests)
        {
            if (requests is null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            var wanted = requests.Sum(r => r.Amount);
            var factor = wanted > 0f ? Math.Min(1f, Stock(product) / wanted) : 0f;
            var toCollect = wanted * factor;
            foreach (var plant in _plants.Values.Where(p => p.Recipe.Product == product))
            {
                toCollect -= plant.Collect(toCollect);
            }

            var result = new List<ShopDelivery>(requests.Count);
            foreach (var request in requests)
            {
                result.Add(new ShopDelivery(request.ShopId, request.Amount, request.Amount * factor));
            }

            return result;
        }

        /// <summary>
        /// Vertaalt de industrie naar de invoer van een <see cref="GoodsChain"/> (E11-08): wat de fabrieken van dit product per week
        /// aan grondstof aankunnen, naast de gewonnen grondstof en de winkelvraag.
        /// </summary>
        public ChainInput ToChainInput(string product, float extractedPerWeek, float shopDemandPerWeek) =>
            new ChainInput(extractedPerWeek, RawCapacityPerDay(product) * 7f, shopDemandPerWeek);

        internal void Restore(int nextId, IEnumerable<ProcessingPlant> plants)
        {
            _plants.Clear();
            foreach (var plant in plants)
            {
                _plants.Add(plant.Id, plant);
            }

            NextId = nextId;
        }
    }
}
