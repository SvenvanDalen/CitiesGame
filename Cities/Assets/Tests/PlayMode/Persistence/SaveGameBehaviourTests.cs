using System;
using System.Collections;
using System.IO;
using Cities.Core.Persistence;
using Cities.Gameplay.Persistence;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cities.Tests.PlayMode.Persistence
{
    /// <summary>Opslaan en laden met de echte componenten: lopende klok en volledige kaart.</summary>
    public class SaveGameBehaviourTests
    {
        private GameObject _root;
        private string _directory;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root);
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator RunningCitySurvivesSaveAndLoad()
        {
            _directory = Path.Combine(Path.GetTempPath(), "cities-playmode-" + Guid.NewGuid().ToString("N"));
            _root = new GameObject("City");
            _root.SetActive(false);
            var simulation = _root.AddComponent<SimulationHostBehaviour>();
            var world = _root.AddComponent<WorldMapHostBehaviour>();
            var save = _root.AddComponent<SaveGameBehaviour>();
            save.Configure(simulation, world, _directory);
            _root.SetActive(true);
            yield return null;

            simulation.Clock.SetSpeed(SimulationSpeed.Fastest);
            yield return new WaitUntilOrTimeout(() => simulation.Clock.Tick >= 20, 5f);
            simulation.Clock.SetSpeed(SimulationSpeed.Paused);
            var map = world.Map;
            map.Height[100, 200] = 42.5f;
            map.Zones.SetForCell(new CellCoord(300, 301), new ZoneCell(ZoneType.Commercial, ZoneDensity.High));
            var savedTick = simulation.Clock.Tick;
            var savedDate = simulation.Clock.Calendar.Date;

            var metadata = save.Saves.Save("Havenstad");

            // De stad verandert verder na het opslaan ...
            simulation.Clock.Step();
            simulation.Clock.SetSpeed(SimulationSpeed.Normal);
            map.Height[100, 200] = 0f;
            map.Zones.SetForCell(new CellCoord(300, 301), ZoneCell.Empty);

            // ... en laden zet alles terug.
            save.Saves.Load("Havenstad");

            Assert.That(simulation.Clock.Tick, Is.EqualTo(savedTick));
            Assert.That(simulation.Clock.Speed, Is.EqualTo(SimulationSpeed.Paused));
            Assert.That(simulation.Clock.Calendar.Date, Is.EqualTo(savedDate));
            Assert.That(map.Height[100, 200], Is.EqualTo(42.5f));
            Assert.That(map.Zones.GetForCell(new CellCoord(300, 301)), Is.EqualTo(new ZoneCell(ZoneType.Commercial, ZoneDensity.High)));
            Assert.That(metadata.Tick, Is.EqualTo(savedTick));
            Assert.That(metadata.Year, Is.EqualTo(savedDate.Year));
            Assert.That(save.Saves.List(), Is.EqualTo(new[] { "Havenstad" }));
            Assert.That(new FileInfo(Path.Combine(_directory, "Havenstad" + SaveFile.Extension)).Length, Is.LessThan(map.MemoryBytes / 10), "Lege kaart comprimeert sterk");

            // De simulatie draait na laden gewoon door.
            simulation.Clock.SetSpeed(SimulationSpeed.Fastest);
            yield return new WaitUntilOrTimeout(() => simulation.Clock.Tick > savedTick + 5, 5f);
            Assert.That(simulation.Clock.Tick, Is.GreaterThan(savedTick + 5));
        }

        private sealed class WaitUntilOrTimeout : CustomYieldInstruction
        {
            private readonly Func<bool> _condition;
            private readonly float _deadline;

            public WaitUntilOrTimeout(Func<bool> condition, float seconds)
            {
                _condition = condition;
                _deadline = Time.realtimeSinceStartup + seconds;
            }

            public override bool keepWaiting => !_condition() && Time.realtimeSinceStartup < _deadline;
        }
    }
}
