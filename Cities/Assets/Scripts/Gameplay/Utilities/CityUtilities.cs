using System;
using System.Collections.Generic;
using Cities.Simulation.Utilities;
using Unity.Mathematics;

namespace Cities.Gameplay.Utilities
{
    public enum PlantKind
    {
        PowerPlant,
        WaterPump,
        SewagePlant,
        Landfill,
    }

    /// <summary>Welk deel van zijn vraag een gebouw krijgt per voorziening, van 0 tot 1.</summary>
    public readonly struct UtilityStatus
    {
        public UtilityStatus(float power, float water, float sewage, float waste)
        {
            Power = power;
            Water = water;
            Sewage = sewage;
            Waste = waste;
        }

        public float Power { get; }

        public float Water { get; }

        public float Sewage { get; }

        public float Waste { get; }

        public float Worst => Math.Min(Math.Min(Power, Water), Math.Min(Sewage, Waste));

        public bool IsConnected => Worst >= 0.999f;
    }

    /// <summary>
    /// De vier nutsnetten van de stad (E26-03): stroom, water, riool en afval als <see cref="UtilityNetwork"/>. Gebouwen vragen per inwoner en
    /// werknemer; centrales, pompen, rioolzuiveringen en stortplaatsen leveren. Een knoop sluit aan op alles binnen de aansluitafstand.
    /// </summary>
    public sealed class CityUtilities
    {
        public const float ConnectRadius = 80f;
        public const float PowerPerPerson = 1.2f;
        public const float WaterPerPerson = 0.15f;
        public const float SewagePerPerson = 0.12f;
        public const float WastePerPerson = 0.002f;
        public const int PeoplePerHousehold = 3;

        private readonly Dictionary<int, (int Power, int Water, int Sewage, int Waste)> _nodes = new Dictionary<int, (int, int, int, int)>();
        private readonly Dictionary<int, (float Power, float Water)> _baseDemand = new Dictionary<int, (float, float)>();
        private int _next;

        public CityUtilities()
        {
            Power = new UtilityNetwork(ConnectRadius);
            Water = new UtilityNetwork(ConnectRadius);
            Sewage = new UtilityNetwork(ConnectRadius);
            Waste = new UtilityNetwork(ConnectRadius);
        }

        public UtilityNetwork Power { get; }

        public UtilityNetwork Water { get; }

        public UtilityNetwork Sewage { get; }

        public UtilityNetwork Waste { get; }

        public int Count => _nodes.Count;

        /// <summary>Capaciteit van één centrale, pomp, zuivering of stortplaats.</summary>
        public static float CapacityOf(PlantKind kind)
        {
            switch (kind)
            {
                case PlantKind.PowerPlant: return 6000f;
                case PlantKind.WaterPump: return 800f;
                case PlantKind.SewagePlant: return 700f;
                default: return 12f;
            }
        }

        /// <summary>Sluit een gebouw aan; de vraag volgt uit huishoudens en arbeidsplaatsen. Geeft de handle terug.</summary>
        public int AddBuilding(float2 position, int households, int workplaces)
        {
            var people = (Math.Max(0, households) * PeoplePerHousehold) + Math.Max(0, workplaces);
            var handle = Register(
                Power.Add(position, 0f, people * PowerPerPerson * PowerFactor),
                Water.Add(position, 0f, people * WaterPerPerson * WaterFactor),
                Sewage.Add(position, 0f, people * SewagePerPerson),
                Waste.Add(position, 0f, people * WastePerPerson));
            _baseDemand[handle] = (people * PowerPerPerson, people * WaterPerPerson);
            return handle;
        }

        /// <summary>Huidige vraagfactor voor stroom (bijvoorbeeld hittegolf of winter), 1 is normaal.</summary>
        public float PowerFactor { get; private set; } = 1f;

        /// <summary>Huidige vraagfactor voor water, 1 is normaal.</summary>
        public float WaterFactor { get; private set; } = 1f;

        /// <summary>Past de vraag van alle aangesloten gebouwen aan; doet niets als de factoren niet veranderen.</summary>
        public void SetDemandFactors(float power, float water)
        {
            power = Math.Max(0.1f, power);
            water = Math.Max(0.1f, water);
            if (Math.Abs(power - PowerFactor) < 0.0001f && Math.Abs(water - WaterFactor) < 0.0001f)
            {
                return;
            }

            PowerFactor = power;
            WaterFactor = water;
            foreach (var pair in _baseDemand)
            {
                var node = _nodes[pair.Key];
                Power.Set(node.Power, 0f, pair.Value.Power * power);
                Water.Set(node.Water, 0f, pair.Value.Water * water);
            }
        }

        public int AddPlant(PlantKind kind, float2 position)
        {
            var capacity = CapacityOf(kind);
            return Register(
                kind == PlantKind.PowerPlant ? Power.Add(position, capacity, 0f) : Power.Add(position, 0f, 0f),
                kind == PlantKind.WaterPump ? Water.Add(position, capacity, 0f) : Water.Add(position, 0f, 0f),
                kind == PlantKind.SewagePlant ? Sewage.Add(position, capacity, 0f) : Sewage.Add(position, 0f, 0f),
                kind == PlantKind.Landfill ? Waste.Add(position, capacity, 0f) : Waste.Add(position, 0f, 0f));
        }

        public void Remove(int handle)
        {
            if (_nodes.TryGetValue(handle, out var n))
            {
                Power.Remove(n.Power);
                Water.Remove(n.Water);
                Sewage.Remove(n.Sewage);
                Waste.Remove(n.Waste);
                _nodes.Remove(handle);
                _baseDemand.Remove(handle);
            }
        }

        public UtilityStatus StatusOf(int handle)
        {
            if (!_nodes.TryGetValue(handle, out var n))
            {
                return new UtilityStatus(1f, 1f, 1f, 1f);
            }

            return new UtilityStatus(Power.Satisfaction(n.Power), Water.Satisfaction(n.Water), Sewage.Satisfaction(n.Sewage), Waste.Satisfaction(n.Waste));
        }

        /// <summary>Rekent alle vier de netten één keer door (voor tests en directe aanroep).</summary>
        public void Tick()
        {
            Power.Tick();
            Water.Tick();
            Sewage.Tick();
            Waste.Tick();
        }

        private int Register(int power, int water, int sewage, int waste)
        {
            var handle = _next++;
            _nodes[handle] = (power, water, sewage, waste);
            return handle;
        }
    }
}
