using System;
using Cities.World.Grid;
using Cities.World.Water;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Plaatst waterbronnen en afvoerpunten. Een klik op een lege plek zet er een neer met het ingestelde debiet; een
    /// klik op een bestaande haalt hem weg. <see cref="Drain"/> kiest tussen toestromen en wegstromen.
    /// </summary>
    public sealed class WaterSourceTool : ITool
    {
        public const float MinDischarge = 1f;
        public const float MaxDischarge = 500f;

        /// <summary>Een klik binnen deze afstand van een bestaande bron raakt die bron.</summary>
        public const float PickRadius = 24f;

        private readonly WaterSources _sources;
        private readonly WorldDimensions _world;
        private float _discharge = 20f;

        public WaterSourceTool(WaterSources sources, WorldDimensions world)
        {
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            _world = world;
        }

        public string Name => Drain ? "Afvoer" : "Waterbron";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        /// <summary><c>true</c> voor een afvoerpunt, <c>false</c> voor een bron.</summary>
        public bool Drain { get; set; }

        /// <summary>Debiet in m³/s, altijd positief; <see cref="Drain"/> bepaalt de richting.</summary>
        public float Discharge
        {
            get => _discharge;
            set => _discharge = math.clamp(value, MinDischarge, MaxDischarge);
        }

        public void Activate()
        {
            // Geen toestand om te onthouden.
        }

        public void Deactivate()
        {
            // Geen toestand om te onthouden.
        }

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            var point = input.Pick.Ground.Point;
            if (!_world.Contains(new Vector2(point.x, point.z)))
            {
                return new ToolPreview(point, 0f, float2.zero, "Buiten de kaart");
            }

            var existing = _sources.Nearest(point.x, point.z, PickRadius);
            var info = existing.HasValue
                ? $"{Describe(existing.Value)} · klik om weg te halen"
                : $"{(Drain ? "Afvoer" : "Bron")} van {_discharge:0} m³/s · klik om te plaatsen";
            return new ToolPreview(point, 0f, float2.zero, null, info);
        }

        public void Apply(in ToolPreview preview)
        {
            var x = preview.Position.x;
            var z = preview.Position.z;
            if (!_sources.RemoveNearest(x, z, PickRadius))
            {
                _sources.Add(x, z, Drain ? -_discharge : _discharge);
            }
        }

        public bool CancelStep() => false;

        private static string Describe(WaterSource source) =>
            $"{(source.IsDrain ? "Afvoer" : "Bron")} van {Math.Abs(source.Discharge):0} m³/s";
    }
}
