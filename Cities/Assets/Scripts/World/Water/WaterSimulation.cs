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
        private int _appliedSources = -1;
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
            Grid = new WaterResample(Width, Depth, Ratio, fine.Width);
            map.HeightChanged += OnHeightChanged;
        }

        private WaterResample Grid { get; }

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

        /// <summary>De bronnen en afvoerpunten; wijzigingen gaan bij de volgende stap in de simulatie.</summary>
        public WaterSources Sources { get; } = new WaterSources();

        /// <summary>Zet de grove toestand (uit een save); de fijne waterdiepte volgt bij de volgende stap.</summary>
        public void Restore(float[] water, float[] flux, WaterSource[] sources)
        {
            _water.CopyFrom(water);
            _flux.CopyFrom(flux);
            Sources.Replace(sources);
            _steps = 0;
        }

        /// <summary>Plant één of meer stappen van samen <paramref name="seconds"/> en het verversen van de fijne diepte.</summary>
        public JobHandle Schedule(float seconds, JobHandle dependency)
        {
            var settings = Settings;
            var cell = _map.Dimensions.CellSize * Ratio;
            var handle = dependency;
            if (_appliedSources != Sources.Version)
            {
                // Synchronisatiepunt: de debietlaag wordt op de main thread herschreven, dus de vorige stap moet klaar zijn.
                handle.Complete();
                ApplySources();
            }

            if (_groundDirty)
            {
                _groundDirty = false;
                handle = new DownsampleJob { Fine = _map.Height.Values, Grid = Grid, Coarse = _ground }
                    .Schedule(Depth, 8, handle);
            }

            var count = Mathf.Clamp(Mathf.CeilToInt(seconds / settings.StepSeconds), 1, 4);
            var step = new WaterStep(Width, Depth, cell, seconds / count);
            for (var i = 0; i < count; i++)
            {
                handle = new FluxJob { Settings = settings, Step = step, Ground = _ground, Water = _water, Flux = _flux }
                    .Schedule(Depth, 8, handle);
                handle = new DepthJob { Settings = settings, Step = step, Ground = _ground, Flux = _flux, Source = _source, Water = _water }
                    .Schedule(Depth, 8, handle);
            }

            if (_steps++ % DisplayEvery == 0)
            {
                var fine = _map.WaterDepth.Layout;
                handle = new UpsampleJob
                {
                    Grid = Grid, CoarseGround = _ground, CoarseWater = _water,
                    FineGround = _map.Height.Values, FineWater = _map.WaterDepth.Values,
                }.Schedule(fine.Depth, 8, handle);
            }

            return handle;
        }

        /// <summary>Zet de lijst bronnen om in debiet per grof hoekpunt; bronnen op dezelfde plek tellen op.</summary>
        private void ApplySources()
        {
            _appliedSources = Sources.Version;
            for (var i = 0; i < _source.Length; i++)
            {
                _source[i] = 0f;
            }

            var cell = _map.Dimensions.CellSize * Ratio;
            foreach (var source in Sources.Items)
            {
                var cx = Mathf.Clamp(Mathf.RoundToInt(source.X / cell), 0, Width - 1);
                var cz = Mathf.Clamp(Mathf.RoundToInt(source.Z / cell), 0, Depth - 1);
                _source[(cz * Width) + cx] += source.Discharge;
            }
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
            public WaterResample Grid;

            [NativeDisableParallelForRestriction] public NativeArray<float> Coarse;

            public void Execute(int index) =>
                ShallowWater.DownsampleRow(index, Grid, Fine.AsReadOnlySpan(), Coarse.AsSpan());
        }

        [BurstCompile]
        private struct FluxJob : IJobParallelFor
        {
            public WaterSettings Settings;
            public WaterStep Step;
            [ReadOnly] public NativeArray<float> Ground;
            [ReadOnly] public NativeArray<float> Water;

            [NativeDisableParallelForRestriction] public NativeArray<float> Flux;

            public void Execute(int index) =>
                ShallowWater.UpdateFlux(index, Step, Settings, Ground.AsReadOnlySpan(), Water.AsReadOnlySpan(), Flux.AsSpan());
        }

        [BurstCompile]
        private struct DepthJob : IJobParallelFor
        {
            public WaterSettings Settings;
            public WaterStep Step;
            [ReadOnly] public NativeArray<float> Ground;
            [ReadOnly] public NativeArray<float> Flux;
            [ReadOnly] public NativeArray<float> Source;

            [NativeDisableParallelForRestriction] public NativeArray<float> Water;

            public void Execute(int index) =>
                ShallowWater.UpdateDepth(index, Step, Settings, Ground.AsReadOnlySpan(), Flux.AsReadOnlySpan(), Source.AsReadOnlySpan(), Water.AsSpan());
        }

        [BurstCompile]
        private struct UpsampleJob : IJobParallelFor
        {
            public WaterResample Grid;
            [ReadOnly] public NativeArray<float> CoarseGround;
            [ReadOnly] public NativeArray<float> CoarseWater;
            [ReadOnly] public NativeArray<float> FineGround;

            [NativeDisableParallelForRestriction] public NativeArray<float> FineWater;

            public void Execute(int index) =>
                ShallowWater.UpsampleRow(index, Grid, CoarseGround.AsReadOnlySpan(), CoarseWater.AsReadOnlySpan(),
                    FineGround.AsReadOnlySpan(), FineWater.AsSpan());
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
