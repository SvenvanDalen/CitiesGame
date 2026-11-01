using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Een celkaart met een waarde per cel (vervuiling, geluid, landwaarde...) die elke stap uitwaaiert naar de buren, met de wind meeschuift en
    /// vervalt (E12-01). De stap is een Burst-job over twee buffers, dus alle milieulagen delen deze ene implementatie.
    /// </summary>
    /// <remarks>
    /// shortcut: aan de rand stroomt er geen verse waarde binnen en is er geen uitwisseling over de rand; upgrade naar een instroomwaarde
    /// per rand zodra een laag dat nodig heeft.
    /// </remarks>
    public sealed class DiffusionField : IDisposable
    {
        private NativeArray<float> _current;
        private NativeArray<float> _next;

        public DiffusionField(int width, int height, Allocator allocator = Allocator.Persistent)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "De kaart heeft minstens één cel nodig.");
            }

            Width = width;
            Height = height;
            _current = new NativeArray<float>(width * height, allocator);
            _next = new NativeArray<float>(width * height, allocator);
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Waarden rij voor rij (index = z * Width + x), voor lezen, opslaan en weergave.</summary>
        public NativeArray<float> Values => _current;

        public float this[int x, int z]
        {
            get => _current[Index(x, z)];
            set => _current[Index(x, z)] = value;
        }

        public float Total
        {
            get
            {
                var sum = 0f;
                for (var i = 0; i < _current.Length; i++)
                {
                    sum += _current[i];
                }

                return sum;
            }
        }

        public void Step(in DiffusionSettings settings)
        {
            new StepJob
            {
                Source = _current,
                Target = _next,
                Width = Width,
                Height = Height,
                Settings = settings,
            }.Schedule(_current.Length, 64).Complete();
            (_current, _next) = (_next, _current);
        }

        public void Dispose()
        {
            if (_current.IsCreated)
            {
                _current.Dispose();
                _next.Dispose();
            }
        }

        private int Index(int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Cel ({x}, {z}) valt buiten de kaart.");
            }

            return (z * Width) + x;
        }

        [BurstCompile]
        private struct StepJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Source;
            [WriteOnly] public NativeArray<float> Target;
            public int Width;
            public int Height;
            public DiffusionSettings Settings;

            public void Execute(int index)
            {
                var n = DiffusionStep.Neighbours(index, Width, Height);
                var neighbours = new float4(Source[n.x], Source[n.y], Source[n.z], Source[n.w]);
                Target[index] = DiffusionStep.Next(Source[index], neighbours, Settings);
            }
        }
    }
}
