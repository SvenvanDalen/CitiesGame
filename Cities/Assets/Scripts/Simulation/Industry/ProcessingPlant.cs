using System;
using System.Collections.Generic;

namespace Cities.Simulation.Industry
{
    public enum PlantStatus : byte
    {
        /// <summary>De fabriek draaide in de laatste stap.</summary>
        Running = 0,

        /// <summary>Er ontbreekt grondstof.</summary>
        StarvedForInput = 1,

        /// <summary>De uitvoeropslag is vol; de afzet haalt het product niet op.</summary>
        OutputFull = 2,

        /// <summary>Geen bezetting (geen personeel of stroom) of nog niet gedraaid.</summary>
        Idle = 3,
    }

    /// <summary>
    /// Een verwerkende fabriek (E14-03): maakt uit grondstoffen een eindproduct volgens een <see cref="Recipe"/>.
    /// De snelheid hangt af van de bezetting (<see cref="Efficiency"/>), de grondstof die voorradig is en de ruimte in de uitvoeropslag.
    /// </summary>
    public sealed class ProcessingPlant
    {
        private readonly float[] _inputStock;

        internal ProcessingPlant(int id, Recipe recipe, float efficiency)
        {
            Id = id;
            Recipe = recipe;
            _inputStock = new float[recipe.Inputs.Count];
            Efficiency = ClampEfficiency(efficiency);
            Status = PlantStatus.Idle;
        }

        public int Id { get; }

        public Recipe Recipe { get; }

        /// <summary>Bezetting 0 tot 1, bijvoorbeeld het aandeel ingevulde banen.</summary>
        public float Efficiency { get; private set; }

        /// <summary>Ton eindproduct die klaarstaat.</summary>
        public float OutputStock { get; private set; }

        public PlantStatus Status { get; private set; }

        /// <summary>Product dat deze plant in de laatste stap maakte.</summary>
        public float LastProduced { get; private set; }

        /// <summary>Grondstof die per dag verwerkt kan worden bij de huidige bezetting, in ton.</summary>
        public float RawCapacityPerDay => Recipe.RawPerCycle * Recipe.CyclesPerDay * Efficiency;

        public void SetEfficiency(float efficiency) => Efficiency = ClampEfficiency(efficiency);

        public float InputStock(string resource)
        {
            var index = IndexOf(resource);
            return index < 0 ? 0f : _inputStock[index];
        }

        /// <summary>Ruimte in de opslag voor een grondstof, in ton.</summary>
        public float InputRoom(string resource)
        {
            var index = IndexOf(resource);
            return index < 0 ? 0f : Math.Max(0f, Recipe.InputCapacity(resource) - _inputStock[index]);
        }

        /// <summary>Neemt grondstof aan tot de opslag vol is en geeft terug hoeveel er echt in ging.</summary>
        public float Accept(string resource, float amount)
        {
            var index = IndexOf(resource);
            if (index < 0 || !(amount > 0f))
            {
                return 0f;
            }

            var taken = Math.Min(amount, InputRoom(resource));
            _inputStock[index] += taken;
            return taken;
        }

        /// <summary>Laat de fabriek <paramref name="days"/> dagen draaien en geeft het geproduceerde aantal ton.</summary>
        public float Step(float days)
        {
            LastProduced = 0f;
            if (!(days > 0f) || !(Efficiency > 0f))
            {
                Status = PlantStatus.Idle;
                return 0f;
            }

            var cycles = Recipe.CyclesPerDay * Efficiency * days;
            var room = (Recipe.OutputCapacity - OutputStock) / Recipe.OutputPerCycle;
            var inputLimit = float.MaxValue;
            for (var i = 0; i < _inputStock.Length; i++)
            {
                inputLimit = Math.Min(inputLimit, _inputStock[i] / Recipe.Inputs[i].Amount);
            }

            var done = Math.Max(0f, Math.Min(cycles, Math.Min(room, inputLimit)));
            for (var i = 0; i < _inputStock.Length; i++)
            {
                _inputStock[i] = Math.Max(0f, _inputStock[i] - done * Recipe.Inputs[i].Amount);
            }

            LastProduced = done * Recipe.OutputPerCycle;
            OutputStock = Math.Min(Recipe.OutputCapacity, OutputStock + LastProduced);
            Status = ClassifyStatus(cycles, room, inputLimit);
            return LastProduced;
        }

        /// <summary>Haalt product op en geeft terug hoeveel er echt meeging.</summary>
        public float Collect(float amount)
        {
            var taken = Math.Max(0f, Math.Min(amount, OutputStock));
            OutputStock -= taken;
            return taken;
        }

        internal void Restore(float efficiency, float outputStock, IReadOnlyList<float> inputStock, PlantStatus status)
        {
            Efficiency = ClampEfficiency(efficiency);
            OutputStock = Math.Min(Recipe.OutputCapacity, Math.Max(0f, outputStock));
            for (var i = 0; i < _inputStock.Length; i++)
            {
                _inputStock[i] = Math.Min(Recipe.InputCapacity(Recipe.Inputs[i].Resource), Math.Max(0f, inputStock[i]));
            }

            Status = status;
            LastProduced = 0f;
        }

        private static PlantStatus ClassifyStatus(float cycles, float room, float inputLimit)
        {
            if (room <= 0f)
            {
                return PlantStatus.OutputFull;
            }

            return inputLimit < cycles ? PlantStatus.StarvedForInput : PlantStatus.Running;
        }

        private static float ClampEfficiency(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));

        private int IndexOf(string resource)
        {
            for (var i = 0; i < Recipe.Inputs.Count; i++)
            {
                if (Recipe.Inputs[i].Resource == resource)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
