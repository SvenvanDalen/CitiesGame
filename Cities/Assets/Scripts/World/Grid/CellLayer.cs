using System;
using Unity.Collections;
using UnityEngine;

namespace Cities.World.Grid
{
    /// <summary>
    /// Eén laag van de wereld: een <see cref="NativeArray{T}"/> met de geometrie uit <see cref="LayerLayout"/>.
    /// Is een struct, zodat de laag (of <see cref="Values"/>) rechtstreeks in Burst-jobs gaat.
    /// </summary>
    public struct CellLayer<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> _values;

        public CellLayer(LayerLayout layout, Allocator allocator, T initialValue = default)
        {
            Layout = layout;
            _values = new NativeArray<T>(layout.Length, allocator);
            if (!initialValue.Equals(default(T)))
            {
                Fill(initialValue);
            }
        }

        public LayerLayout Layout { get; }

        public bool IsCreated => _values.IsCreated;

        /// <summary>Ruwe data, rij voor rij (index = z * Width + x), voor jobs en opslaan.</summary>
        public NativeArray<T> Values => _values;

        public T this[int x, int z]
        {
            get => _values[Layout.Index(x, z)];
            set => _values[Layout.Index(x, z)] = value;
        }

        /// <summary>De waarde die een wereldcel dekt (bij een grovere laag gedeeld met de buurcellen).</summary>
        public T GetForCell(CellCoord cell) => _values[Layout.IndexOfCell(cell)];

        public void SetForCell(CellCoord cell, T value) => _values[Layout.IndexOfCell(cell)] = value;

        public void Fill(T value)
        {
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] = value;
            }
        }

        public void CopyFrom(CellLayer<T> source)
        {
            if (source.Layout != Layout)
            {
                throw new ArgumentException($"Lagen verschillen: {source.Layout} tegenover {Layout}.", nameof(source));
            }

            _values.CopyFrom(source._values);
        }

        public void Dispose()
        {
            if (_values.IsCreated)
            {
                _values.Dispose();
            }
        }
    }

    public static class CellLayerExtensions
    {
        /// <summary>Bilineair geïnterpoleerde waarde op een wereldpositie, bijvoorbeeld de terreinhoogte onder een voertuig.</summary>
        public static float Sample(this CellLayer<float> layer, Vector2 position)
        {
            var sample = layer.Layout.GetBilinear(position);
            var values = layer.Values;
            return sample.Interpolate(values[sample.Index00], values[sample.Index10], values[sample.Index01], values[sample.Index11]);
        }

        /// <summary>Kleinste en grootste waarde in de laag.</summary>
        public static (float Min, float Max) Range(this CellLayer<float> layer)
        {
            var values = layer.Values;
            if (values.Length == 0)
            {
                return (0f, 0f);
            }

            var min = values[0];
            var max = values[0];
            for (var i = 1; i < values.Length; i++)
            {
                min = Math.Min(min, values[i]);
                max = Math.Max(max, values[i]);
            }

            return (min, max);
        }
    }
}
