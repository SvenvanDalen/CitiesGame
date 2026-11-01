using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Cities.Simulation.Disasters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class DisasterManagerTests
    {
        private static readonly float2 Area = new float2(1000f, 1000f);

        private static DisasterSpec Fire(float chance = 0f) => new DisasterSpec("Brand", chance, 1, 2, 1, 100, 10);

        private static long Day(DisasterManager manager)
        {
            var random = new DeterministicRandom(1);
            return manager.Step(ref random, Area);
        }

        [Test]
        public void ADisasterWalksThroughWarningEffectAftermathAndEnds()
        {
            var manager = new DisasterManager();
            manager.Register(Fire());
            var phases = new List<DisasterPhase>();
            manager.PhaseChanged += d => phases.Add(d.Phase);
            var disaster = manager.Trigger("Brand", new float2(5f, 6f));

            var costs = new List<long>();
            for (var i = 0; i < 5; i++)
            {
                costs.Add(Day(manager));
            }

            Assert.That(phases, Is.EqualTo(new[] { DisasterPhase.Warning, DisasterPhase.Active, DisasterPhase.Aftermath, DisasterPhase.Ended }));
            Assert.That(costs, Is.EqualTo(new long[] { 0, 100, 100, 10, 0 }), "schade tijdens het effect, opruimen in de nasleep");
            Assert.That(manager.Running, Is.Empty);
            Assert.That(disaster.Phase, Is.EqualTo(DisasterPhase.Ended));
            Assert.That(disaster.Position, Is.EqualTo(new float2(5f, 6f)));
        }

        [Test]
        public void PhasesOfZeroDaysAreSkipped()
        {
            var manager = new DisasterManager();
            manager.Register(new DisasterSpec("Meteoriet", 0f, 0, 1, 0, 50, 0));

            var disaster = manager.Trigger("Meteoriet", float2.zero);

            Assert.That(disaster.Phase, Is.EqualTo(DisasterPhase.Active), "geen waarschuwing");
            Assert.That(Day(manager), Is.EqualTo(50));
            Assert.That(manager.Running, Is.Empty, "geen nasleep");
        }

        [Test]
        public void RandomDisastersFollowTheSeedAndCanBeSwitchedOff()
        {
            var manager = new DisasterManager();
            manager.Register(Fire(1f));
            var random = new DeterministicRandom(7);

            manager.Step(ref random, Area);
            Assert.That(manager.Running, Has.Count.EqualTo(1));
            Assert.That(manager.Running[0].Position.x, Is.InRange(0f, 1000f));

            manager.Step(ref random, Area);
            Assert.That(manager.Running, Has.Count.EqualTo(1), "een type loopt maar één keer tegelijk");

            var off = new DisasterManager();
            off.Register(Fire(1f));
            off.RandomEnabled = false;
            off.Step(ref random, Area);
            Assert.That(off.Running, Is.Empty);
            Assert.That(off.Trigger("Brand", float2.zero), Is.Not.Null, "handmatig kan altijd");

            var typeOff = new DisasterManager();
            typeOff.Register(Fire(1f));
            typeOff.SetEnabled("Brand", false);
            typeOff.Step(ref random, Area);
            Assert.That(typeOff.IsEnabled("Brand"), Is.False);
            Assert.That(typeOff.Running, Is.Empty);
            typeOff.SetEnabled("Brand", true);
            Assert.That(typeOff.IsEnabled("Brand"), Is.True);
        }

        [Test]
        public void TheSameSeedGivesTheSameDisasters()
        {
            List<float2> Run()
            {
                var manager = new DisasterManager();
                manager.Register(Fire(0.5f));
                var starts = new List<float2>();
                manager.PhaseChanged += d => starts.Add(d.Position);
                var random = new DeterministicRandom(99);
                for (var i = 0; i < 20; i++)
                {
                    manager.Step(ref random, Area);
                }

                return starts;
            }

            var a = Run();

            Assert.That(a, Is.Not.Empty);
            Assert.That(Run(), Is.EqualTo(a));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var manager = new DisasterManager();
            manager.Register(Fire());

            Assert.Throws<ArgumentNullException>(() => manager.Register(null));
            Assert.Throws<ArgumentException>(() => manager.Register(Fire()), "dubbele naam");
            Assert.Throws<ArgumentException>(() => manager.Trigger("Onbekend", float2.zero));
            Assert.Throws<ArgumentException>(() => new DisasterSpec(" ", 0f, 0, 1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisasterSpec("x", 2f, 0, 1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisasterSpec("x", 0f, -1, 1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisasterSpec("x", 0f, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DisasterSpec("x", 0f, 0, 1, 0, -1, 0));
            Assert.That(manager.Specs, Has.Count.EqualTo(1));
        }
    }
}
