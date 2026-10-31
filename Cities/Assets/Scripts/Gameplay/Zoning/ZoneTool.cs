using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Gameplay.Tools;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>Hoe de <see cref="ZoneTool"/> cellen kiest.</summary>
    public enum ZoneMode
    {
        /// <summary>Een ronde kwast die doorwerkt zolang de knop ingedrukt is.</summary>
        Brush,

        /// <summary>Twee klikken: de hoeken van een rechthoek.</summary>
        Rectangle,

        /// <summary>Eén klik: alle aaneengesloten bebouwbare cellen om de aangewezen cel.</summary>
        Fill,
    }

    /// <summary>
    /// Zoneert cellen langs wegen (E05-02) met een kwast, een rechthoek of vullen, of wist ze (<see cref="Erase"/>).
    /// De preview toont de cellen die veranderen (groen, rood als een cel niet mag) en de bebouwbare cellen om de muis heen.
    /// </summary>
    public sealed class ZoneTool : ITool
    {
        public const float MinRadius = 8f;
        public const float MaxRadius = 64f;
        public const int FillLimit = 400;

        private const int HintCells = 6;

        private readonly ZoneService _service;
        private readonly WorldMap _map;
        private readonly ZonePreviewOverlay _overlay;
        private readonly List<CellCoord> _cells = new List<CellCoord>();
        private CellCoord? _anchor;
        private CellCoord _hover = new CellCoord(-1, -1);
        private float _radius = 16f;

        public ZoneTool(ZoneService service, WorldMap map, ZonePreviewOverlay overlay)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public ZoneMode Mode { get; set; } = ZoneMode.Brush;

        public ZoneType Type { get; set; } = ZoneType.Residential;

        public ZoneDensity Density { get; set; } = ZoneDensity.Low;

        /// <summary>Wist in plaats van zoneren.</summary>
        public bool Erase { get; set; }

        public float Radius
        {
            get => _radius;
            set => _radius = math.clamp(value, MinRadius, MaxRadius);
        }

        public string Name => Erase ? "Zone wissen" : $"Zone {Type}";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => Mode == ZoneMode.Brush;

        public void Activate() => Reset();

        public void Deactivate() => Reset();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            if (!input.Pick.HasGround)
            {
                _overlay.Clear();
                return ToolPreview.Hidden;
            }

            var point = input.Pick.Ground.Point;
            var size = _map.Dimensions.CellSize;
            var cell = new CellCoord((int)math.floor(point.x / size), (int)math.floor(point.z / size));
            if (!_map.Zones.Layout.Contains(cell.X, cell.Z))
            {
                _overlay.Clear();
                return new ToolPreview(point, 0f, float2.zero, "Buiten de kaart");
            }

            Collect(cell, point.xz, size);
            Draw(cell);
            var count = _cells.Count;
            var reason = count == 0 ? Why(cell) : null;
            return new ToolPreview(point, 0f, float2.zero, reason, count > 0 ? $"{count} cellen" : null);
        }

        public void Apply(in ToolPreview preview)
        {
            if (Mode == ZoneMode.Rectangle && !_anchor.HasValue)
            {
                _anchor = _hover;
                return;
            }

            if (Erase)
            {
                _service.Clear(_cells);
            }
            else
            {
                _service.Paint(_cells, Type, Density);
            }

            _anchor = null;
            _cells.Clear();
        }

        public bool CancelStep()
        {
            var was = _anchor.HasValue;
            _anchor = null;
            return was;
        }

        private void Reset()
        {
            _anchor = null;
            _cells.Clear();
            _overlay.Clear();
        }

        private void Collect(CellCoord cell, float2 point, float size)
        {
            _hover = cell;
            _cells.Clear();
            switch (Mode)
            {
                case ZoneMode.Brush:
                    _cells.AddRange(ZoneShapes.Disc(point, _radius, size));
                    break;
                case ZoneMode.Rectangle:
                    _cells.AddRange(ZoneShapes.Rectangle(_anchor ?? cell, cell));
                    break;
                default:
                    _cells.AddRange(ZoneShapes.Fill(cell, Fillable, FillLimit));
                    break;
            }
        }

        // shortcut: vult en kleurt per frame opnieuw en loopt per cel langs alle segmenten; bij grote netten per cel cachen.
        private void Draw(CellCoord centre)
        {
            _overlay.Clear();
            for (var z = centre.Z - HintCells; z <= centre.Z + HintCells; z++)
            {
                for (var x = centre.X - HintCells; x <= centre.X + HintCells; x++)
                {
                    var hint = new CellCoord(x, z);
                    if (_map.Zones.Layout.Contains(x, z) && _service.Reason(hint) == null)
                    {
                        _overlay.Add(hint, DebugColor.Neutral, _map.Dimensions.CellSize * 0.4f);
                    }
                }
            }

            foreach (var cell in _cells)
            {
                _overlay.Add(cell, CellColor(cell));
            }
        }

        private DebugColor CellColor(CellCoord cell)
        {
            if (Erase)
            {
                return DebugColor.Warning;
            }

            return _service.Reason(cell) == null ? DebugColor.Good : DebugColor.Bad;
        }

        private bool Fillable(CellCoord cell)
        {
            if (!_map.Zones.Layout.Contains(cell.X, cell.Z))
            {
                return false;
            }

            var zone = _map.Zones.GetForCell(cell);
            return Erase ? zone.IsZoned : _service.Reason(cell) == null && zone != new ZoneCell(Type, Density);
        }

        private string Why(CellCoord cell) => Erase ? "Geen zone om te wissen" : _service.Reason(cell) ?? "Al gezoned";
    }
}
