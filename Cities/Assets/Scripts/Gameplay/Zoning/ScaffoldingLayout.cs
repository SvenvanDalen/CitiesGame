using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// De steiger om een gebouw in aanbouw (E05-06) als lijst balken, zoals een gevelsteiger van buizen en planken: staanders om de
    /// 2,5 m, een leuning en een tussenregel per bouwlaag van 2,4 m en een plankenvloer om de andere bouwlaag. Het midden van de
    /// onderkant ligt op de oorsprong.
    /// </summary>
    public static class ScaffoldingLayout
    {
        public const float PoleSpacing = 2.5f;
        public const float LiftHeight = 2.4f;
        public const float Tube = 0.05f;
        public const float BoardWidth = 0.6f;
        public const float BoardThickness = 0.04f;

        /// <summary>De balken van een steiger rond een rechthoek van <paramref name="width"/> × <paramref name="depth"/> meter en <paramref name="height"/> hoog.</summary>
        public static List<(Vector3 Min, Vector3 Max)> Boxes(float width, float depth, float height)
        {
            if (width <= 0f || depth <= 0f || height <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Breedte, diepte en hoogte moeten positief zijn.");
            }

            var boxes = new List<(Vector3, Vector3)>();
            var hx = width * 0.5f;
            var hz = depth * 0.5f;
            var top = height + LiftHeight;
            AddPoles(boxes, -hx, hx, -hz, top, true);
            AddPoles(boxes, -hz, hz, hx, top, false);
            AddPoles(boxes, -hx, hx, hz, top, true);
            AddPoles(boxes, -hz, hz, -hx, top, false);

            var lift = 0;
            for (var y = LiftHeight; y <= top; y += LiftHeight)
            {
                lift++;
                AddRail(boxes, hx, hz, y);
                AddRail(boxes, hx, hz, y - (LiftHeight * 0.5f));
                if (lift % 2 == 1)
                {
                    AddDeck(boxes, hx, hz, y);
                }
            }

            return boxes;
        }

        private static void AddPoles(List<(Vector3, Vector3)> boxes, float from, float to, float fixedAxis, float top, bool alongX)
        {
            var count = Mathf.Max(1, Mathf.CeilToInt((to - from) / PoleSpacing));
            for (var i = 0; i <= count; i++)
            {
                var at = Mathf.Lerp(from, to, i / (float)count);
                var x = alongX ? at : fixedAxis;
                var z = alongX ? fixedAxis : at;
                boxes.Add((new Vector3(x - (Tube * 0.5f), 0f, z - (Tube * 0.5f)), new Vector3(x + (Tube * 0.5f), top, z + (Tube * 0.5f))));
            }
        }

        private static void AddRail(List<(Vector3, Vector3)> boxes, float hx, float hz, float y)
        {
            var t = Tube * 0.5f;
            boxes.Add((new Vector3(-hx, y - t, -hz - t), new Vector3(hx, y + t, -hz + t)));
            boxes.Add((new Vector3(-hx, y - t, hz - t), new Vector3(hx, y + t, hz + t)));
            boxes.Add((new Vector3(-hx - t, y - t, -hz), new Vector3(-hx + t, y + t, hz)));
            boxes.Add((new Vector3(hx - t, y - t, -hz), new Vector3(hx + t, y + t, hz)));
        }

        private static void AddDeck(List<(Vector3, Vector3)> boxes, float hx, float hz, float y)
        {
            var low = y - BoardThickness;
            boxes.Add((new Vector3(-hx, low, -hz), new Vector3(hx, y, -hz + BoardWidth)));
            boxes.Add((new Vector3(-hx, low, hz - BoardWidth), new Vector3(hx, y, hz)));
            boxes.Add((new Vector3(-hx, low, -hz + BoardWidth), new Vector3(-hx + BoardWidth, y, hz - BoardWidth)));
            boxes.Add((new Vector3(hx - BoardWidth, low, -hz + BoardWidth), new Vector3(hx, y, hz - BoardWidth)));
        }
    }
}
