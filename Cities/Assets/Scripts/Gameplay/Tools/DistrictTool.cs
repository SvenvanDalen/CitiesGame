using System;
using Cities.World.Districts;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Penseel om districten op de kaart te schilderen (E13-01). Met <see cref="Erase"/> haalt het blokken uit hun district,
    /// zodat ze weer bij de hele stad horen. Werkt door zolang de knop ingedrukt is.
    /// </summary>
    public sealed class DistrictTool : IBrushTool
    {
        public const float MinRadius = 16f;
        public const float MaxRadius = 512f;

        private readonly DistrictMap _map;
        private float _radius = 96f;

        public DistrictTool(DistrictMap map) => _map = map ?? throw new ArgumentNullException(nameof(map));

        public string Name => Erase ? "District wissen" : "District schilderen";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => true;

        /// <summary>Het district dat geschilderd wordt; 0 is nog niets gekozen.</summary>
        public int ActiveDistrict { get; set; }

        /// <summary><c>true</c> om blokken uit hun district te halen in plaats van te schilderen.</summary>
        public bool Erase { get; set; }

        public float Radius
        {
            get => _radius;
            set => _radius = math.clamp(value, MinRadius, MaxRadius);
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
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            var point = input.Pick.Ground.Point;
            return new ToolPreview(point, 0f, float2.zero, InvalidReason(point.xz));
        }

        public void Apply(in ToolPreview preview) =>
            _map.Paint(preview.Position.xz, _radius, Erase ? DistrictBook.WholeCity : ActiveDistrict);

        public bool CancelStep() => false;

        private string InvalidReason(float2 point)
        {
            var size = new float2(_map.Dimensions.SizeX, _map.Dimensions.SizeZ);
            if (math.any(point < 0f) || math.any(point > size))
            {
                return "Buiten de kaart";
            }

            return !Erase && !_map.Book.Contains(ActiveDistrict) ? "Kies eerst een district" : null;
        }
    }
}
