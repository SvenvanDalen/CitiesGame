using System;
using Cities.Simulation.Scheduling;
using Cities.World.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Cities.World.Water
{
    /// <summary>
    /// De watersimulatie van de wereld: rekent op een grover raster dan de terreinmesh (32 m, vier cellen per waarde) en
    /// schrijft het resultaat als waterdiepte op 8 m terug in <see cref="WorldMap.WaterDepth"/>. Het grove water en de
    /// stromen zijn de echte toestand (zie <see cref="WaterSection"/>); de fijne diepte is afgeleid.
    /// </summary>
    public sealed class WaterSimulation : IDisposable
    {
        /// <summary>Vier cellen van 8 m per waarde: 32 m.</summary>
        public const int Ratio = WorldMap.CoarseCellsPerSample;

        /// <summary>De fijne waterdiepte wordt om de zoveel stappen ververst; de simulatie zelf draait elke stap.</summary>
        public const int DisplayEvery = 5;

        private readonly WorldMap _map;
        private NativeArray<float> _ground;
        private NativeArray<float> _water;
        private NativeArray<float> _flux;
        private NativeArray<float> _source;
        private bool _groundDirty = true;
        private int _steps;

        public WaterSimulation(WorldMap map, WaterSettings settings)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            Settings = settings;
            var fine = map.Height.Layout;
            if ((fine.Width - 1) % Ratio != 0 || (fine.Depth - 1) % Ratio != 0)
            {
                throw new ArgumentException("Kaart is niet deelbaar door het waterraster.", nameof(map));
            }

            Width = ((fine.Width - 1) / Ratio) + 1;
            Depth = ((fine.Depth - 1) / Ratio) + 1;
            _ground = new NativeArray<float>(Width * Depth, Allocator.Persistent);
            _water = new NativeArray<float>(Width * Depth, Allocator.Persistent);
            _flux = new NativeArray<float>(Width * Depth * ShallowWater.Directions, Allocator.Persistent);
            _source = new NativeArray<float>(Width * Depth, Allocator.Persistent);
            map.HeightChanged += OnHeightChanged;
        }

        public WaterSettings Settings { get; set; }

        public int Width { get; }

        public int Depth { get; }

        /// <summary>Waterdiepte per grof hoekpunt in meters.</summary>
        public NativeArray<float> Water => _water;

        /// <summary>Stromen per grof hoekpunt (−x, +x, −z, +z) in m³/s.</summary>
        public NativeArray<float> Flux => _flux;

        /// <summary>Totale hoeveelheid water in m³.</summary>
        public double Volume
        {
            get
            {
                var cell = _map.Dimensions.CellSize * Ratio;
                double total = 0;
                foreach (var depth in _water)
                {
                    total += depth;
                }

                return total * cell * cell;
            }
        }

        /// <summary>Laat <paramref name="cubicMetersPerSecond"/> toestromen op het grove hoekpunt bij wereldpositie (x, z); 0 stopt de bron.</summary>
        public void SetSource(float x, float z, float cubicMetersPerSecond)
        {
            var cell = _map.Dimensions.CellSize * Ratio;
            var cx = Mathf.Clamp(Mathf.RoundToInt(x / cell), 0, Width - 1);
            var cz = Mathf.Clamp(Mathf.RoundToInt(z / cell), 0, Depth - 1);
            _source[(cz * Width) + cx] = Mathf.Max(0f, cubicMetersPerSecond);
        }

        /// <summary>Zet de grove toestand (uit een save); de fijne waterdiepte volgt bij de volgende stap.</summary>
        public void Restore(float[] water, float[] flux)
        {
            _water.CopyFrom(water);
            _flux.CopyFrom(flux);
            _steps = 0;
        }

        /// <summary>Plant één of meer stappen van samen <paramref name="seconds"/> en het verversen van de fijne diepte.</summary>
        public JobHandle Schedule(float seconds, JobHandle dependency)
        {
            var settings = Settings;
            var cell = _map.Dimensions.CellSize * Ratio;
            var handle = dependency;
            if (_groundDirty)
            {
                _groundDirty = false;
                handle = new DownsampleJob { Fine = _map.Height.Values, FineWidth = _map.Height.Layout.Width, Coarse = _ground, Width = Width }
                    .Schedule(Depth, 8, handle);
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(seconds / settings.StepSeconds), 1, 4);
            var dt = seconds / count;
            for (var i = 0; i < count; i++)
            {
                handle = new FluxJob { Settings = settings, Width = Width, Depth = Depth, CellSize = cell, Dt = dt, Ground = _ground, Water = _water, Flux = _flux }
                    .Schedule(Depth, 8, handle);
                handle = new DepthJob { Settings = settings, Width = Width, Depth = Depth, CellSize = cell, Dt = dt, Ground = _ground, Flux = _flux, Source = _source, Water = _water }
                    .Schedule(Depth, 8, handle);
            }

            if (_steps++ % DisplayEvery == 0)
            {
                var fine = _map.WaterDepth.Layout;
                handle = new UpsampleJob
                {
                    CoarseGround = _ground, CoarseWater = _water, Width = Width, Depth = Depth,
                    FineGround = _map.Height.Values, FineWidth = fine.Width, FineWater = _map.WaterDepth.Values,
                }.Schedule(fine.Depth, 8, handle);
            }

            return handle;
        }

        public void Dispose()
        {
            _map.HeightChanged -= OnHeightChanged;
            _ground.Dispose();
            _water.Dispose();
            _flux.Dispose();
            _source.Dispose();
        }

        private void OnHeightChanged(RectInt corners) => _groundDirty = true;

        [BurstCompile]
        private struct DownsampleJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Fine;
            public int FineWidth;
            public int Width;

            [NativeDisableParallelForRestriction] public NativeArray<float> Coarse;

            public void Execute(int row) =>
                ShallowWater.DownsampleRow(row, Fine.AsReadOnlySpan(), FineWidth, Ratio, Width, Coarse.AsSpan());
        }

        [BurstCompile]
        private struct FluxJob : IJobParallelFor
        {
            public WaterSettings Settings;
            public int Width;
            public int Depth;
            public float CellSize;
            public float Dt;
            [ReadOnly] public NativeArray<float> Ground;
            [ReadOnly] public NativeArray<float> Water;

            [NativeDisableParallelForRestriction] public NativeArray<float> Flux;

            public void Execute(int row) =>
                ShallowWater.UpdateFlux(row, Width, Depth, CellSize, Dt, Settings, Ground.AsReadOnlySpan(), Water.AsReadOnlySpan(), Flux.AsSpan());
        }

        [BurstCompile]
        private struct DepthJob : IJobParallelFor
        {
            public WaterSettings Settings;
            public int Width;
            public int Depth;
            public float CellSize;
            public float Dt;
            [ReadOnly] public NativeArray<float> Ground;
            [ReadOnly] public NativeArray<float> Flux;
            [ReadOnly] public NativeArray<float> Source;

            [NativeDisableParallelForRestriction] public NativeArray<float> Water;

            public void Execute(int row) =>
                ShallowWater.UpdateDepth(row, Width, Depth, CellSize, Dt, Settings, Ground.AsReadOnlySpan(), Flux.AsReadOnlySpan(), Source.AsReadOnlySpan(), Water.AsSpan());
        }

        [BurstCompile]
        private struct UpsampleJob : IJobParallelFor
        {
            public int Width;
            public int Depth;
            public int FineWidth;
            [ReadOnly] public NativeArray<float> CoarseGround;
            [ReadOnly] public NativeArray<float> CoarseWater;
            [ReadOnly] public NativeArray<float> FineGround;

            [NativeDisableParallelForRestriction] public NativeArray<float> FineWater;

            public void Execute(int row) =>
                ShallowWater.UpsampleRow(row, CoarseGround.AsReadOnlySpan(), CoarseWater.AsReadOnlySpan(), Width, Depth, Ratio,
                    FineGround.AsReadOnlySpan(), FineWidth, FineWater.AsSpan());
        }
    }

    /// <summary>Het watersysteem in de simulatieloop.</summary>
    public sealed class WaterSystem : ISimulationSystem
    {
        private readonly WaterSimulation _simulation;

        public WaterSystem(WaterSimulation simulation) => _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

        public string Name => "Water";

        public JobHandle Schedule(in SimulationTick tick, JobHandle dependency) =>
            _simulation.Schedule(tick.StepSeconds, dependency);
    }
}
