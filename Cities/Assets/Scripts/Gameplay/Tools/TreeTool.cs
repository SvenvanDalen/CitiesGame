using System;
using Cities.World.Grid;
using Cities.World.Trees;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Penseel om bomen te planten of te kappen. Werkt door zolang de knop ingedrukt is; <see cref="Strength"/> is het
    /// deel van een vol bos dat er per seconde in het midden bij komt of af gaat.
    /// </summary>
    public sealed class TreeTool : IBrushTool
    {
        public const float MinRadius = 4f;
        public const float MaxRadius = 128f;
        public const float MinStrength = 0.1f;
        public const float MaxStrength = 4f;

        private readonly WorldMap _map;
        private float _radius = 24f;
        private float _strength = 1f;
        private float _deltaTime;

        public TreeTool(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public string Name => Plant ? "Bomen planten" : "Bomen kappen";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => true;

        /// <summary><c>true</c> om te planten, <c>false</c> om te kappen.</summary>
        public bool Plant { get; set; } = true;

        public float Radius
        {
            get => _radius;
            set => _radius = math.clamp(value, MinRadius, MaxRadius);
        }

        public float Strength
        {
            get => _strength;
            set => _strength = math.clamp(value, MinStrength, MaxStrength);
        }

        public void Activate()
        {
            // Een penseel houdt niets vast.
        }

        public void Deactivate()
        {
            // Een penseel houdt niets vast.
        }

        /// <summary>Het midden van het penseel op de grond; de rand tekent de terreinshader.</summary>
        public ToolPreview UpdatePreview(in ToolInput input)
        {
            _deltaTime = input.DeltaTime;
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            var point = input.Pick.Ground.Point;
            var size = new float2(_map.Dimensions.SizeX, _map.Dimensions.SizeZ);
            var outside = math.any(point.xz < 0f) || math.any(point.xz > size);
            return new ToolPreview(point, 0f, float2.zero, outside ? "Buiten de kaart" : null);
        }

        public void Apply(in ToolPreview preview)
        {
            var changed = TreeBrush.Apply(_map, Plant, preview.Position.xz, _radius, 255f * _strength * _deltaTime);
            if (changed.width > 0 && changed.height > 0)
            {
                _map.NotifyTreesChanged(changed);
            }
        }

        public bool CancelStep() => false;
    }
}
