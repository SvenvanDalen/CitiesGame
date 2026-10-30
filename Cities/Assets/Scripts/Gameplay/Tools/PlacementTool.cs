using System;
using Cities.World.Grid;
using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Plaatst een object met een vaste footprint, gesnapt op het cellenraster en draaibaar per kwartslag.</summary>
    public sealed class PlacementTool : ITool
    {
        public const string OutsideMap = "Buiten de kaart";
        public const string TooSteep = "Te steil";

        private readonly WorldDimensions _dimensions;
        private readonly ITerrainHeight _terrain;
        private readonly IPlacementTarget _target;
        private readonly float2[] _samples = new float2[5];
        private Footprint _footprint;

        /// <param name="maxRise">Grootste hoogteverschil binnen de footprint in meters.</param>
        public PlacementTool(
            string name,
            int width,
            int depth,
            WorldDimensions dimensions,
            ITerrainHeight terrain,
            IPlacementTarget target,
            float maxRise = 1.5f)
        {
            if (width < 1 || depth < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(depth), "Minstens één cel per richting.");
            }

            if (!(maxRise >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxRise), maxRise, "Het hoogteverschil kan niet negatief zijn.");
            }

            Name = name;
            Width = width;
            Depth = depth;
            MaxRise = maxRise;
            _dimensions = dimensions;
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public string Name { get; }

        /// <summary>Model voor de preview; <c>null</c> toont alleen de footprint.</summary>
        public GameObject Ghost { get; set; }

        public bool RepeatsWhileHeld => false;

        public int Width { get; }

        public int Depth { get; }

        public float MaxRise { get; }

        public int QuarterTurns { get; private set; }

        public void Activate() => QuarterTurns = 0;

        public void Deactivate()
        {
            // Niets op te ruimen: de preview hoort bij de controller.
        }

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            QuarterTurns = (((QuarterTurns + input.RotateSteps) % 4) + 4) % 4;
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            _footprint = GridSnap.Footprint(input.Pick.Ground.Point, Width, Depth, QuarterTurns, _dimensions.CellSize);
            var center = _footprint.Center(_dimensions);
            var reason = Check(_footprint, out var baseHeight);
            var size = new float2(Width, Depth) * _dimensions.CellSize;
            return new ToolPreview(new float3(center.x, baseHeight, center.y), _footprint.YawDegrees, size, reason);
        }

        public void Apply(in ToolPreview preview) => _target.Place(_footprint, preview.Position);

        public bool CancelStep() => false;

        /// <summary>Kaartrand, helling en wat er al staat; de basis is het laagste meetpunt, zodat niets zweeft.</summary>
        private string Check(in Footprint footprint, out float baseHeight)
        {
            var min = _dimensions.CellMin(footprint.Origin);
            var max = _dimensions.CellMin(footprint.Max) + new Vector2(_dimensions.CellSize, _dimensions.CellSize);
            var center = footprint.Center(_dimensions);
            _samples[0] = new float2(min.x, min.y);
            _samples[1] = new float2(max.x, min.y);
            _samples[2] = new float2(min.x, max.y);
            _samples[3] = new float2(max.x, max.y);
            _samples[4] = new float2(center.x, center.y);

            var low = float.MaxValue;
            var high = float.MinValue;
            foreach (var sample in _samples)
            {
                var height = _terrain.SurfaceHeight(sample);
                low = math.min(low, height);
                high = math.max(high, height);
            }

            baseHeight = low;
            if (!footprint.IsInside(_dimensions))
            {
                return OutsideMap;
            }

            return high - low > MaxRise ? TooSteep : _target.Check(footprint);
        }
    }
}
