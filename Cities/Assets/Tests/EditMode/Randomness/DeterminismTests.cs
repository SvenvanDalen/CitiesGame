using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Simulation.Commands;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Randomness
{
    /// <summary>
    /// "Zelfde seed en input geven dezelfde uitkomst": een kleine stad met wandelaars die door commands verschijnen en
    /// elke tick een willekeurige stap zetten, gecontroleerd met <see cref="StateChecksum"/>.
    /// </summary>
    public class DeterminismTests
    {
        private const int Ticks = 400;

        [Test]
        public void SameSeedAndCommandsGiveTheSameCity()
        {
            var first = Run(seed: 2024);
            var second = Run(seed: 2024);

            Assert.That(StateChecksum.FirstDifference(first.Checksums, second.Checksums), Is.Null);
            Assert.That(first.Walkers, Is.GreaterThan(10), "De test moet echt iets simuleren");
        }

        [Test]
        public void AnotherSeedGivesAnotherCity()
        {
            var first = Run(seed: 2024);
            var other = Run(seed: 2025);

            Assert.That(StateChecksum.FirstDifference(first.Checksums, other.Checksums), Is.EqualTo(RandomSection.SectionId));
            Assert.That(Walkers(first.Checksums).Hash, Is.Not.EqualTo(Walkers(other.Checksums).Hash));
        }

        [Test]
        public void ProcessingOrderDoesNotMatter()
        {
            // Parallelle jobs verwerken objecten in willekeurige volgorde; per object een eigen generator maakt dat onschadelijk.
            var forward = Run(seed: 7);
            var backward = Run(seed: 7, reverseOrder: true);

            Assert.That(StateChecksum.FirstDifference(forward.Checksums, backward.Checksums), Is.Null);
        }

        [Test]
        public void ALoadedCityContinuesExactlyLikeTheOriginal()
        {
            var uninterrupted = Run(seed: 99);

            var city = new TestCity(seed: 99);
            city.Advance(Ticks / 2);
            byte[] save;
            using (var stream = new MemoryStream())
            {
                SaveFile.Write(stream, new SaveMetadata(), city.Sections);
                save = stream.ToArray();
            }

            var restored = new TestCity(seed: 1);
            using (var stream = new MemoryStream(save))
            {
                SaveFile.Read(stream, restored.Sections);
            }

            restored.Advance(Ticks - (Ticks / 2));

            Assert.That(restored.Random.Seed, Is.EqualTo(99UL));
            Assert.That(StateChecksum.FirstDifference(uninterrupted.Checksums, StateChecksum.Compute(restored.Sections)), Is.Null);
        }

        private static SectionChecksum Walkers(IEnumerable<SectionChecksum> checksums) =>
            checksums.Single(c => c.Id == WalkerSection.SectionId);

        private static (IReadOnlyList<SectionChecksum> Checksums, int Walkers) Run(ulong seed, bool reverseOrder = false)
        {
            var city = new TestCity(seed, reverseOrder);
            city.Advance(Ticks);
            Assert.That(city.Runner.ScheduledTicks, Is.EqualTo(Ticks));
            return (StateChecksum.Compute(city.Sections), city.Walkers.Count);
        }

        private struct SpawnWalker : ISimulationCommand
        {
        }

        /// <summary>Klok, commands, seed en wandelaars; elke 20 ticks vraagt de "speler" een nieuwe wandelaar aan.</summary>
        private sealed class TestCity : ITickScheduler, ICommandHandler<SpawnWalker>
        {
            private readonly SimulationClock _clock;
            private readonly CommandBus _commands = new CommandBus();

            private readonly bool _reverseOrder;
            private readonly RandomStreamKey _spawnStream;
            private readonly RandomStreamKey _stepStream;

            public TestCity(ulong seed, bool reverseOrder = false)
            {
                _reverseOrder = reverseOrder;
                _clock = new SimulationClock(SimulationClockSettings.Default);
                Random = new SimulationRandom(seed);
                _spawnStream = Random.Register("Test.Verschijnen");
                _stepStream = Random.Register("Test.Stap");
                _commands.RegisterHandler(this);
                Runner = new SimulationRunner(_clock, _commands, this, Random);
                Sections = new ISaveSection[]
                {
                    new SimulationClockSection(_clock, _commands),
                    new RandomSection(Random),
                    new WalkerSection(Walkers),
                };
            }

            public SimulationRandom Random { get; }

            public SimulationRunner Runner { get; }

            public List<float2> Walkers { get; } = new List<float2>();

            public ISaveSection[] Sections { get; }

            public void Advance(int ticks)
            {
                for (var i = 0; i < ticks; i++)
                {
                    if (_clock.Tick % 20 == 0)
                    {
                        _commands.Enqueue(new SpawnWalker());
                    }

                    // Step in plaats van Update: frametijd hoort de uitkomst niet te bepalen, alleen ticks en commands.
                    _clock.Step();
                }
            }

            public void ScheduleTick(in SimulationTick tick)
            {
                var source = tick.Random(_stepStream);
                for (var n = 0; n < Walkers.Count; n++)
                {
                    var i = _reverseOrder ? Walkers.Count - 1 - n : n;
                    var random = source.At(tick.Tick, i);
                    Walkers[i] += random.NextInsideUnitCircle() * random.NextFloat(0.5f, 1.5f);
                }
            }

            public void ExecuteOnMainThread(Action work) => work();

            public CommandValidation Validate(in SpawnWalker command) => CommandValidation.Valid();

            public void Execute(in SpawnWalker command)
            {
                var random = Random.Source(_spawnStream).At(_clock.Tick, Walkers.Count);
                Walkers.Add(new float2(random.NextFloat(0f, 1000f), random.NextFloat(0f, 1000f)));
            }
        }

        private sealed class WalkerSection : ISaveSection
        {
            public const string SectionId = "WALK";

            private readonly List<float2> _walkers;

            public WalkerSection(List<float2> walkers) => _walkers = walkers;

            public string Id => SectionId;

            public int Version => 1;

            public void Write(SaveWriter writer) => writer.WriteArray<float2>(_walkers.ToArray());

            public Action Read(SaveReader reader, int version)
            {
                var walkers = reader.ReadArray<float2>(1 << 20);
                reader.ExpectEnd();
                return () =>
                {
                    _walkers.Clear();
                    _walkers.AddRange(walkers);
                };
            }
        }
    }
}
