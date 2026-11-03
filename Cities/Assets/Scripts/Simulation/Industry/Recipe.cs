using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Industry
{
    /// <summary>Eén grondstof die een recept per productiecyclus verbruikt.</summary>
    public readonly struct RecipeInput
    {
        public RecipeInput(string resource, float amount)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException("Een invoer heeft een grondstofnaam nodig.", nameof(resource));
            }

            if (!(amount > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "De hoeveelheid per cyclus is positief.");
            }

            Resource = resource;
            Amount = amount;
        }

        public string Resource { get; }

        /// <summary>Ton per cyclus.</summary>
        public float Amount { get; }
    }

    /// <summary>
    /// Wat een verwerkende fabriek maakt (E14-03): per cyclus gaan de invoeren erin en komt er <see cref="OutputPerCycle"/> product uit.
    /// De opslag van invoer en uitvoer wordt in cycli uitgedrukt, zodat een grote fabriek automatisch meer buffer heeft.
    /// </summary>
    public sealed class Recipe
    {
        private readonly RecipeInput[] _inputs;

        public Recipe(string product, IEnumerable<RecipeInput> inputs, float outputPerCycle, float cyclesPerDay, float inputBufferCycles, float outputBufferCycles)
        {
            if (string.IsNullOrWhiteSpace(product))
            {
                throw new ArgumentException("Een recept heeft een product nodig.", nameof(product));
            }

            _inputs = (inputs ?? throw new ArgumentNullException(nameof(inputs))).ToArray();
            if (_inputs.Length == 0 || _inputs.Select(i => i.Resource).Distinct().Count() != _inputs.Length)
            {
                throw new ArgumentException("Een recept heeft minstens één invoer en geen dubbele grondstoffen.", nameof(inputs));
            }

            if (!(outputPerCycle > 0f) || !(cyclesPerDay > 0f) || !(inputBufferCycles >= 1f) || !(outputBufferCycles >= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(outputPerCycle), "Opbrengst en snelheid zijn positief en de buffers minstens één cyclus.");
            }

            Product = product;
            OutputPerCycle = outputPerCycle;
            CyclesPerDay = cyclesPerDay;
            InputBufferCycles = inputBufferCycles;
            OutputBufferCycles = outputBufferCycles;
        }

        public string Product { get; }

        public IReadOnlyList<RecipeInput> Inputs => _inputs;

        /// <summary>Ton product per cyclus.</summary>
        public float OutputPerCycle { get; }

        /// <summary>Cycli per dag bij volle bezetting.</summary>
        public float CyclesPerDay { get; }

        public float InputBufferCycles { get; }

        public float OutputBufferCycles { get; }

        /// <summary>Maximale opslag van het eindproduct, in ton.</summary>
        public float OutputCapacity => OutputPerCycle * OutputBufferCycles;

        /// <summary>Totale grondstof (ton) die één cyclus verbruikt.</summary>
        public float RawPerCycle => _inputs.Sum(i => i.Amount);

        /// <summary>Opslag voor één grondstof, in ton; 0 als het recept die niet gebruikt.</summary>
        public float InputCapacity(string resource)
        {
            foreach (var input in _inputs)
            {
                if (input.Resource == resource)
                {
                    return input.Amount * InputBufferCycles;
                }
            }

            return 0f;
        }
    }

    /// <summary>De recepten van het spel, op productnaam.</summary>
    public sealed class RecipeBook
    {
        private readonly SortedDictionary<string, Recipe> _recipes = new SortedDictionary<string, Recipe>(StringComparer.Ordinal);

        public IEnumerable<Recipe> Recipes => _recipes.Values;

        public Recipe Add(Recipe recipe)
        {
            if (recipe is null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (_recipes.ContainsKey(recipe.Product))
            {
                throw new InvalidOperationException("Dit product heeft al een recept: " + recipe.Product);
            }

            _recipes.Add(recipe.Product, recipe);
            return recipe;
        }

        public bool TryGet(string product, out Recipe recipe)
        {
            recipe = null;
            return product != null && _recipes.TryGetValue(product, out recipe);
        }

        public Recipe Get(string product) =>
            TryGet(product, out var recipe) ? recipe : throw new KeyNotFoundException("Onbekend product: " + product);
    }
}
