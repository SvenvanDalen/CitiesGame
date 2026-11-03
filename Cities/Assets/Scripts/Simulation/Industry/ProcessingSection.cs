using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Industry
{
    /// <summary>Sectie <c>PROC</c>: de fabrieken van de verwerkende industrie met hun voorraden (E14-03). De recepten komen uit de configuratie.</summary>
    public sealed class ProcessingSection : ISaveSection
    {
        public const string SectionId = "PROC";

        private readonly ProcessingIndustry _industry;

        public ProcessingSection(ProcessingIndustry industry)
        {
            _industry = industry ?? throw new ArgumentNullException(nameof(industry));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_industry.NextId);
            var plants = _industry.Plants.ToArray();
            writer.Write(plants.Length);
            foreach (var plant in plants)
            {
                writer.Write(plant.Id);
                writer.Write(plant.Recipe.Product);
                writer.Write(plant.Efficiency);
                writer.Write(plant.OutputStock);
                writer.Write((byte)plant.Status);
                foreach (var input in plant.Recipe.Inputs)
                {
                    writer.Write(plant.InputStock(input.Resource));
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count < 0 || count > ProcessingIndustry.MaxPlants)
            {
                throw reader.Corrupt($"onmogelijk aantal fabrieken: {count}");
            }

            var plants = new List<ProcessingPlant>();
            var previousId = 0;
            for (var i = 0; i < count; i++)
            {
                var plant = ReadPlant(reader, previousId);
                previousId = plant.Id;
                plants.Add(plant);
            }

            reader.ExpectEnd();
            if (nextId <= previousId)
            {
                throw reader.Corrupt("het volgnummer van de fabrieken is te laag");
            }

            return () => _industry.Restore(nextId, plants);
        }

        private ProcessingPlant ReadPlant(SaveReader reader, int previousId)
        {
            var id = reader.ReadInt32();
            var product = reader.ReadString();
            var efficiency = reader.ReadSingle();
            var output = reader.ReadSingle();
            var status = reader.ReadByte();
            if (id <= previousId)
            {
                throw reader.Corrupt("fabrieken staan niet op oplopend ID");
            }

            if (!_industry.Recipes.TryGet(product, out var recipe))
            {
                throw reader.Corrupt("onbekend recept: " + product);
            }

            var inputs = new float[recipe.Inputs.Count];
            for (var i = 0; i < inputs.Length; i++)
            {
                inputs[i] = reader.ReadSingle();
            }

            if (!(efficiency >= 0f && efficiency <= 1f) || !(output >= 0f) || inputs.Any(v => !(v >= 0f)) || status > (byte)PlantStatus.Idle)
            {
                throw reader.Corrupt("ongeldige fabriek " + id);
            }

            var plant = new ProcessingPlant(id, recipe, efficiency);
            plant.Restore(efficiency, output, inputs, (PlantStatus)status);
            return plant;
        }
    }
}
