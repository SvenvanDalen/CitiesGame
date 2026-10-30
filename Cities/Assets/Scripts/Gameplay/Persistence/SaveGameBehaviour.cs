using System;
using System.IO;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Persistence
{
    /// <summary>
    /// Koppelt het opslaan aan de scene: de klok uit <see cref="SimulationHostBehaviour"/> en de kaart uit
    /// <see cref="WorldMapHostBehaviour"/>. Saves staan in <c>Application.persistentDataPath/Saves</c>. Nieuwe systemen
    /// met status voegen hier hun sectie toe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveGameBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private string _cityName = "Nieuwe stad";

        [Tooltip("Naam van de save voor de contextmenu-opdrachten in de inspector.")]
        [SerializeField] private string _slot = "Test";

        private string _directory;

        public static string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public SaveGameService Saves { get; private set; }

        /// <summary>Koppelt de onderdelen vanuit code (tests); <paramref name="directory"/> vervangt de standaardmap.</summary>
        internal void Configure(SimulationHostBehaviour simulation, WorldMapHostBehaviour world, string directory)
        {
            _simulation = simulation;
            _world = world;
            _directory = directory;
        }

        private void Start()
        {
            var clock = _simulation.Clock;
            var sections = new ISaveSection[]
            {
                new SimulationClockSection(clock, _simulation.Commands),
                new RandomSection(_simulation.Random),
                new WorldMapSection(_world.Map),
            };
            Saves = new SaveGameService(new SaveStorage(_directory ?? SavesDirectory), sections, metadata => Describe(clock, metadata), _simulation.Scheduler.Complete)
            {
                CityName = _cityName,
                GameVersion = Application.version,
            };
        }

        private void Update() => Saves?.AddPlayTime(Time.unscaledDeltaTime);

        [ContextMenu("Opslaan")]
        private void SaveSlot() => Run(() => Saves.Save(_slot));

        [ContextMenu("Laden")]
        private void LoadSlot() => Run(() => Saves.Load(_slot));

        private static void Describe(SimulationClock clock, SaveMetadata metadata)
        {
            var date = clock.Calendar.Date;
            metadata.Tick = clock.Tick;
            metadata.Year = date.Year;
            metadata.Month = date.Month;
            metadata.Day = date.Day;
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is IOException || exception is SaveFormatException || exception is UnauthorizedAccessException)
            {
                Debug.LogError(exception.Message);
            }
        }
    }
}
