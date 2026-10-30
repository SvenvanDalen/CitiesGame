using System;
using Cities.World.Grid;
using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Penseel om het terrein te vormen: omhoog, omlaag, vlak, glad of naar een hoogte. Werkt door zolang de knop
    /// ingedrukt is; <see cref="Strength"/> is in meter per seconde in het midden van het penseel.
    /// </summary>
    public sealed class TerraformTool : ITool
    {
        public const float MinRadius = 8f;
        public const float MaxRadius = 256f;
        public const float MinStrength = 0.5f;
        public const float MaxStrength = 40f;

        private readonly WorldMap _map;
        private float _radius = 48f;
        private float _strength = 6f;
        private float _targetHeight = 40f;
        private float _deltaTime;
        private int _frame;
        private int _lastApplyFrame = -1;
        private float _strokeHeight;

        public TerraformTool(WorldMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public string Name => "Terrein";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => true;

        public TerraformMode Mode { get; set; }

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

        /// <summary>Doel voor <see cref="TerraformMode.SetHeight"/>, in meters.</summary>
        public float TargetHeight
        {
            get => _targetHeight;
            set => _targetHeight = math.clamp(value, TerrainBrush.MinHeight, TerrainBrush.MaxHeight);
        }

        public void Activate() => _lastApplyFrame = -1;

        public void Deactivate()
        {
            // Een penseel houdt niets vast.
        }

        /// <summary>Het midden van het penseel op de grond; de rand tekent de terreinshader.</summary>
        public ToolPreview UpdatePreview(in ToolInput input)
        {
            _frame++;
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
            var center = preview.Position.xz;
            if (_lastApplyFrame != _frame - 1)
            {
                // Een nieuwe streek: vlak maken gaat naar de hoogte waar de muis begon.
                _strokeHeight = _map.Height.Sample(new Vector2(center.x, center.y));
            }

            _lastApplyFrame = _frame;
            var target = Mode == TerraformMode.SetHeight ? _targetHeight : _strokeHeight;
            var changed = TerrainBrush.Apply(_map, Mode, center, _radius, _strength * _deltaTime, target);
            if (changed.width > 0 && changed.height > 0)
            {
                _map.NotifyHeightChanged(changed);
            }
        }

        public bool CancelStep() => false;
    }
}
