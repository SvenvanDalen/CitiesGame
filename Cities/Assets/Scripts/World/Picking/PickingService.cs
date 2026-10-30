using System;
using System.Collections.Generic;

namespace Cities.World.Picking
{
    /// <summary>
    /// Kiest wat er onder de cursor ligt. Eerst bepalen de massieve lagen (terrein, gebouwen) hoe ver de straal
    /// zichtbaar is; daarna wint van alle treffers binnen dat zicht de hoogste <see cref="PickKind"/>, en bij gelijke
    /// soort de dichtstbijzijnde.
    /// </summary>
    /// <remarks>
    /// Wegen en agents liggen op het terrein en zouden zonder marge net achter het terreinpunt kunnen vallen; daarom
    /// telt een treffer tot <see cref="AbsoluteTolerance"/> plus <see cref="RelativeTolerance"/> maal de afstand achter
    /// het eerste massieve oppervlak nog als zichtbaar. Massieve lagen verbergen ook wat een tool niet zoekt: een
    /// wegtool pakt geen knooppunt achter een gebouw.
    /// </remarks>
    public sealed class PickingService
    {
        private readonly List<IPickLayer> _layers = new List<IPickLayer>();
        private readonly List<PickHit> _solidHits = new List<PickHit>();

        public PickingService(float absoluteTolerance = 0.75f, float relativeTolerance = 0.002f)
        {
            if (absoluteTolerance < 0f || float.IsNaN(absoluteTolerance))
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteTolerance), absoluteTolerance, "De marge kan niet negatief zijn.");
            }

            if (relativeTolerance < 0f || float.IsNaN(relativeTolerance))
            {
                throw new ArgumentOutOfRangeException(nameof(relativeTolerance), relativeTolerance, "De marge kan niet negatief zijn.");
            }

            AbsoluteTolerance = absoluteTolerance;
            RelativeTolerance = relativeTolerance;
        }

        public float AbsoluteTolerance { get; }

        public float RelativeTolerance { get; }

        public IReadOnlyList<IPickLayer> Layers => _layers;

        /// <summary>Voegt een laag toe; dispose het resultaat om hem weer te verwijderen.</summary>
        public IDisposable AddLayer(IPickLayer layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            if (layer.Kind == PickKind.None)
            {
                throw new ArgumentException("Een laag heeft een soort nodig.", nameof(layer));
            }

            if (_layers.Contains(layer))
            {
                throw new ArgumentException("Deze laag is al toegevoegd.", nameof(layer));
            }

            _layers.Add(layer);
            return new Registration(this, layer);
        }

        public PickResult Pick(in PickRay ray, PickKinds mask)
        {
            var visible = FindVisibleDistance(ray, out var ground);
            var limit = Math.Min(ray.Length, visible + Tolerance(visible));
            return new PickResult(Choose(ray, mask, limit), ground);
        }

        /// <summary>
        /// Eerst het terrein, daarna de andere massieve lagen alleen tot waar het zicht al reikt: zo zoekt een
        /// gebouwenlaag niet de hele kaart af als de straal vlak voor de camera de grond raakt. Bewaart de treffers.
        /// </summary>
        private float FindVisibleDistance(in PickRay ray, out PickHit ground)
        {
            var visible = ray.Length;
            ground = PickHit.None;
            _solidHits.Clear();
            foreach (var layer in _layers)
            {
                var hit = PickHit.None;
                if (layer.Kind == PickKind.Terrain && layer.IsSolid && layer.Raycast(ray, visible, out hit))
                {
                    visible = Math.Min(visible, hit.Distance);
                    ground = hit;
                }

                _solidHits.Add(hit);
            }

            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.Kind != PickKind.Terrain && layer.IsSolid && layer.Raycast(ray, Math.Min(ray.Length, visible + Tolerance(visible)), out var hit))
                {
                    visible = Math.Min(visible, hit.Distance);
                    _solidHits[i] = hit;
                }
            }

            return visible;
        }

        /// <summary>Massieve lagen hebben hun dichtstbijzijnde treffer al; de rest zoekt alleen binnen het zicht.</summary>
        private PickHit Choose(in PickRay ray, PickKinds mask, float limit)
        {
            var best = PickHit.None;
            for (var i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (!mask.Includes(layer.Kind))
                {
                    continue;
                }

                var hit = _solidHits[i];
                var found = layer.IsSolid ? hit.IsHit : layer.Raycast(ray, limit, out hit);
                if (found && hit.Distance <= limit && IsBetter(hit, best))
                {
                    best = hit;
                }
            }

            return best;
        }

        private float Tolerance(float distance) => AbsoluteTolerance + (distance * RelativeTolerance);

        private static bool IsBetter(PickHit candidate, PickHit best) =>
            candidate.Kind > best.Kind || (candidate.Kind == best.Kind && candidate.Distance < best.Distance);

        private sealed class Registration : IDisposable
        {
            private PickingService _owner;
            private readonly IPickLayer _layer;

            public Registration(PickingService owner, IPickLayer layer)
            {
                _owner = owner;
                _layer = layer;
            }

            public void Dispose()
            {
                _owner?._layers.Remove(_layer);
                _owner = null;
            }
        }
    }
}
