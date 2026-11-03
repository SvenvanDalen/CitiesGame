using System;
using System.Collections;
using System.IO;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Simulation.Network;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using Cities.World.Water;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Tooltip("Optioneel: het wegennet.")]
        [SerializeField] private RoadNetworkHostBehaviour _network;

        [Tooltip("Optioneel: de watersimulatie.")]
        [SerializeField] private WaterHostBehaviour _water;

        [Tooltip("Optioneel: de camera met bladwijzers.")]
        [SerializeField] private Cities.Gameplay.Cameras.CityCameraBehaviour _camera;
        [SerializeField] private string _cityName = "Nieuwe stad";

        [Tooltip("Naam van de save voor de contextmenu-opdrachten in de inspector.")]
        [SerializeField] private string _slot = "Test";

        [Tooltip("Seconden echte speeltijd tussen autosaves; 0 zet autosave uit.")]
        [SerializeField, Min(0f)] private float _autosaveIntervalSeconds = 600f;

        [Range(1, SaveSlots.MaxAutosaveCount)]
        [SerializeField] private int _autosaveCount = SaveSlots.DefaultAutosaveCount;

        [Tooltip("Breedte van de miniatuur naast elke save.")]
        [SerializeField, Range(64, 512)] private int _thumbnailWidth = 256;

        private readonly AutosaveTimer _autosave = new AutosaveTimer();
        private string _directory;
        private bool _busy;

        public static string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public SaveGameService Saves { get; private set; }

        public SaveSlots Slots { get; private set; }

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
            var sections = new System.Collections.Generic.List<ISaveSection>
            {
                new SimulationClockSection(clock, _simulation.Commands),
                new RandomSection(_simulation.Random),
                new WorldMapSection(_world.Map),
            };
            if (_network != null)
            {
                sections.Add(new RoadNetworkSection(_network.Network));
                sections.Add(new TrafficRulesSection(_network.Rules, _network.Network.Contains));
                sections.Add(new Cities.Simulation.Vehicles.ParkingSection(_network.Parking));
            }

            if (_water != null)
            {
                sections.Add(new WaterSection(_water.Water));
            }

            if (_camera != null)
            {
                sections.Add(new Cities.Gameplay.Cameras.CameraBookmarksSection(_camera.Bookmarks));
            }

            var storage = new SaveStorage(_directory ?? SavesDirectory);
            Saves = new SaveGameService(storage, sections, metadata => Describe(clock, metadata), _simulation.Scheduler.Complete)
            {
                CityName = _cityName,
                GameVersion = Application.version,
            };
            Slots = new SaveSlots(Saves, storage) { AutosaveCount = _autosaveCount };
            _autosave.IntervalSeconds = _autosaveIntervalSeconds;
        }

        private void Update()
        {
            if (Saves == null)
            {
                return;
            }

            Saves.AddPlayTime(Time.unscaledDeltaTime);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                RequestSave(SaveSlotKind.Quicksave);
            }
            else if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && Slots.HasQuicksave)
            {
                Run(() => Slots.LoadQuicksave());
            }
            else if (_autosave.Advance(Time.unscaledDeltaTime))
            {
                RequestSave(SaveSlotKind.Autosave);
            }
        }

        /// <summary>Slaat aan het eind van het frame op, met een miniatuur van het beeld.</summary>
        public void RequestSave(SaveSlotKind kind, string manualName = null)
        {
            if (!_busy && Slots != null)
            {
                StartCoroutine(SaveWithThumbnail(kind, manualName));
            }
        }

        private IEnumerator SaveWithThumbnail(SaveSlotKind kind, string manualName)
        {
            _busy = true;
            yield return new WaitForEndOfFrame();
            var thumbnail = CaptureThumbnail();
            Run(() =>
            {
                switch (kind)
                {
                    case SaveSlotKind.Quicksave:
                        Slots.Quicksave(thumbnail);
                        break;
                    case SaveSlotKind.Autosave:
                        Slots.Autosave(thumbnail);
                        break;
                    default:
                        Slots.SaveManual(manualName ?? _slot, thumbnail);
                        break;
                }
            });
            _autosave.Reset();
            _busy = false;
        }

        private byte[] CaptureThumbnail()
        {
            var screen = ScreenCapture.CaptureScreenshotAsTexture();
            if (screen == null)
            {
                return null;
            }

            var height = Mathf.Max(1, Mathf.RoundToInt(_thumbnailWidth * (float)screen.height / screen.width));
            var target = RenderTexture.GetTemporary(_thumbnailWidth, height, 0);
            var previous = RenderTexture.active;
            var small = new Texture2D(_thumbnailWidth, height, TextureFormat.RGB24, false);
            try
            {
                Graphics.Blit(screen, target);
                RenderTexture.active = target;
                small.ReadPixels(new Rect(0, 0, _thumbnailWidth, height), 0, 0);
                small.Apply(false);
                return small.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(screen);
                Destroy(small);
            }
        }

        [ContextMenu("Opslaan")]
        private void SaveSlot() => RequestSave(SaveSlotKind.Manual);

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
