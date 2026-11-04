using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Cities.Simulation.Disasters;
using Unity.Mathematics;

namespace Cities.Gameplay.Audio
{
    /// <summary>Wat de camera hoort (E20-02): hoogte boven de grond en wat er in de omgeving ligt.</summary>
    public readonly struct AmbientContext
    {
        public AmbientContext(float cameraHeight, float urban, float nature, float water, float windSpeed)
        {
            CameraHeight = cameraHeight;
            Urban = urban;
            Nature = nature;
            Water = water;
            WindSpeed = windSpeed;
        }

        /// <summary>Hoogte van de camera boven de grond in meter.</summary>
        public float CameraHeight { get; }

        /// <summary>Bebouwingsdichtheid rond het punt onder de camera, 0 tot 1.</summary>
        public float Urban { get; }

        /// <summary>Aandeel bomen, gras en natuur, 0 tot 1.</summary>
        public float Nature { get; }

        /// <summary>Aandeel water, 0 tot 1.</summary>
        public float Water { get; }

        /// <summary>Windsnelheid in m/s.</summary>
        public float WindSpeed { get; }
    }

    /// <summary>De volumes (0 tot 1) van de vier omgevingslagen.</summary>
    public readonly struct AmbientLevels
    {
        public AmbientLevels(float city, float nature, float water, float wind)
        {
            City = city;
            Nature = nature;
            Water = water;
            Wind = wind;
        }

        public float City { get; }

        public float Nature { get; }

        public float Water { get; }

        public float Wind { get; }
    }

    /// <summary>
    /// Omgevingsgeluid per locatie (E20-02): stadsdrukte, natuur, water en wind. Dicht bij de grond hoor je wat er rondom ligt; hoger op
    /// vervagen de grondgeluiden naar een zachte stadsdreun en neemt de wind toe.
    /// </summary>
    public static class AmbientMix
    {
        /// <summary>Hoogte waaronder de grondgeluiden vol klinken, en hoogte waarboven ze op hun minimum zijn.</summary>
        public const float GroundFadeStart = 40f;
        public const float GroundFadeEnd = 600f;

        /// <summary>Windsnelheid waarbij de windlaag vol klinkt.</summary>
        public const float FullWind = 20f;

        /// <summary>Minimale stadsdreun hoog boven een stad.</summary>
        public const float CityHumFloor = 0.25f;

        public static AmbientLevels Compute(AmbientContext context)
        {
            var ground = 1f - math.smoothstep(GroundFadeStart, GroundFadeEnd, math.max(0f, context.CameraHeight));
            var urban = math.saturate(context.Urban);
            var city = urban * math.max(ground, CityHumFloor);
            var nature = math.saturate(context.Nature) * ground * (1f - (urban * 0.7f));
            var water = math.saturate(context.Water) * ground;
            var wind = math.saturate(context.WindSpeed / FullWind) * (0.3f + (0.7f * (1f - ground)));
            return new AmbientLevels(city, nature, water, wind);
        }
    }

    /// <summary>Waar een bron vandaan klinkt voor de luisteraar (E20-06): links/rechts, hoe hard en of hij achter de camera ligt.</summary>
    public readonly struct SpatialSound
    {
        public SpatialSound(float pan, float gain, bool behind)
        {
            Pan = pan;
            Gain = gain;
            Behind = behind;
        }

        /// <summary>-1 volledig links, 1 volledig rechts.</summary>
        public float Pan { get; }

        public float Gain { get; }

        public bool Behind { get; }
    }

    /// <summary>Richtingsgevoel voor sirenes en alarmen (E20-06) en de keuze van het rampgeluid.</summary>
    public static class DirectionalAudio
    {
        /// <summary>Plaatst een bron op de kaart (x,z) ten opzichte van de luisteraar die naar <paramref name="listenerForward"/> kijkt.</summary>
        public static SpatialSound Place(float2 listener, float2 listenerForward, float2 source, float minDistance, float maxDistance)
        {
            if (math.lengthsq(listenerForward) <= 0f)
            {
                throw new ArgumentException("De kijkrichting heeft lengte nodig.", nameof(listenerForward));
            }

            var distance = math.distance(listener, source);
            var gain = AudioAttenuation.Gain(distance, minDistance, maxDistance);
            if (distance <= 0.0001f)
            {
                return new SpatialSound(0f, gain, false);
            }

            var forward = math.normalize(listenerForward);
            var toSource = (source - listener) / distance;
            var rightX = forward.y;
            var rightY = -forward.x;
            var right = new float2(rightX, rightY);
            return new SpatialSound(math.clamp(math.dot(toSource, right), -1f, 1f), gain, math.dot(toSource, forward) < 0f);
        }

        /// <summary>Het alarm bij een rampfase: waarschuwing een sirene, tijdens het effect een continu alarm, in de nasleep een einde-alarm.</summary>
        public static string AlarmFor(DisasterPhase phase) => phase switch
        {
            DisasterPhase.Warning => "alert.siren-warning",
            DisasterPhase.Active => "alert.alarm-active",
            DisasterPhase.Aftermath => "alert.all-clear",
            _ => null,
        };

        /// <summary>Prioriteit van een rampgeluid in het <see cref="VoiceBudget"/>: rampen en hulpdiensten gaan boven verkeer.</summary>
        public static int Priority(DisasterPhase phase) => phase == DisasterPhase.Ended ? 0 : 100;

        public const int EmergencyVehiclePriority = 80;
    }

    public enum UiSound : byte
    {
        Click,
        Build,
        Demolish,
        Error,
        Notification,
    }

    /// <summary>Wat er voor een interfacegeluid gespeeld moet worden.</summary>
    public readonly struct UiSoundPlay
    {
        public UiSoundPlay(bool play, int variant)
        {
            Play = play;
            Variant = variant;
        }

        public bool Play { get; }

        /// <summary>Welke variant (0-gebaseerd) van het geluid; wisselt zodat hetzelfde geluid niet steeds identiek klinkt.</summary>
        public int Variant { get; }
    }

    /// <summary>
    /// De interfacegeluiden (E20-04): klik, bouw, sloop, fout en melding. Elk geluid heeft een minimale tijd tussen twee keer spelen (zodat een
    /// versleepte bouwtool geen machinegeweer wordt) en een aantal varianten dat om de beurt wordt gekozen.
    /// </summary>
    public sealed class UiSoundBank
    {
        private static readonly float[] Cooldowns = { 0.05f, 0.12f, 0.12f, 0.4f, 1.0f };
        private static readonly int[] Variants = { 3, 2, 2, 1, 2 };

        private readonly double[] _lastPlayed = { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };
        private readonly int[] _next = new int[5];

        public static float CooldownOf(UiSound sound) => Cooldowns[Index(sound)];

        public static int VariantsOf(UiSound sound) => Variants[Index(sound)];

        /// <summary>Vraagt het geluid op tijdstip <paramref name="now"/> (seconden); binnen de wachttijd is het stil.</summary>
        public UiSoundPlay Request(UiSound sound, double now)
        {
            var i = Index(sound);
            if (now - _lastPlayed[i] < Cooldowns[i])
            {
                return new UiSoundPlay(false, 0);
            }

            _lastPlayed[i] = now;
            var variant = _next[i];
            _next[i] = (variant + 1) % Variants[i];
            return new UiSoundPlay(true, variant);
        }

        private static int Index(UiSound sound) =>
            Enum.IsDefined(typeof(UiSound), sound) ? (int)sound : throw new ArgumentOutOfRangeException(nameof(sound), sound, "Onbekend interfacegeluid.");
    }

    public enum MusicContext : byte
    {
        Day,
        Night,
        Disaster,
    }

    /// <summary>Een muziekstuk (E20-05): voor welke momenten het past en hoe lang het duurt.</summary>
    public sealed class MusicTrack
    {
        public MusicTrack(string id, float seconds, params MusicContext[] contexts)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Een stuk heeft een id nodig.", nameof(id));
            }

            if (!(seconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Een stuk duurt langer dan nul seconden.");
            }

            if (contexts == null || contexts.Length == 0)
            {
                throw new ArgumentException("Een stuk past bij minstens één moment.", nameof(contexts));
            }

            Id = id;
            Seconds = seconds;
            Contexts = contexts;
        }

        public string Id { get; }

        public float Seconds { get; }

        public IReadOnlyList<MusicContext> Contexts { get; }
    }

    /// <summary>
    /// De muziekregie (E20-05): een afspeellijst met overgangen. De regisseur kiest een stuk dat bij het moment past (dag, nacht, ramp),
    /// vermijdt de laatste twee gespeelde stukken en gaat vloeiend over (<see cref="FadeSeconds"/>, bij een ramp meteen sneller met
    /// <see cref="DisasterFadeSeconds"/>). De keuze volgt een seed, dus een herhaling klinkt hetzelfde.
    /// </summary>
    public sealed class MusicDirector
    {
        public const float FadeSeconds = 4f;
        public const float DisasterFadeSeconds = 1f;
        public const int RecentMemory = 2;

        private readonly IReadOnlyList<MusicTrack> _tracks;
        private readonly List<string> _recent = new List<string>();
        private DeterministicRandom _random;
        private float _fadeLength = FadeSeconds;
        private float _fadeProgress = 1f;

        public MusicDirector(IReadOnlyList<MusicTrack> tracks, ulong seed)
        {
            _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            _random = new DeterministicRandom(seed);
        }

        /// <summary>Het stuk dat nu klinkt (of null zonder stukken).</summary>
        public MusicTrack Current { get; private set; }

        /// <summary>Het stuk dat wegvloeit tijdens een overgang, anders null.</summary>
        public MusicTrack Outgoing { get; private set; }

        /// <summary>Seconden dat het huidige stuk speelt.</summary>
        public float Elapsed { get; private set; }

        public float CurrentGain => Current == null ? 0f : _fadeProgress;

        public float OutgoingGain => Outgoing == null ? 0f : 1f - _fadeProgress;

        public MusicContext Context { get; private set; } = MusicContext.Day;

        /// <summary>Eén stap van <paramref name="deltaSeconds"/> in het gegeven moment.</summary>
        public void Update(float deltaSeconds, MusicContext context)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "De tijd loopt vooruit.");
            }

            var changed = context != Context;
            Context = context;
            Elapsed += deltaSeconds;
            if (Outgoing != null)
            {
                _fadeProgress = Math.Min(1f, _fadeProgress + (deltaSeconds / _fadeLength));
                if (_fadeProgress >= 1f)
                {
                    Outgoing = null;
                }
            }

            var ending = Current != null && Elapsed >= Current.Seconds - FadeSeconds;
            var unsuitable = Current != null && changed && !Current.Contexts.Contains(context);
            if (Current == null || ending || unsuitable)
            {
                Switch(context);
            }
        }

        private void Switch(MusicContext context)
        {
            var next = Pick(context);
            if (next == null || (Current != null && next.Id == Current.Id && _tracks.Count == 1))
            {
                return;
            }

            Outgoing = Current;
            Current = next;
            Elapsed = 0f;
            _fadeLength = context == MusicContext.Disaster ? DisasterFadeSeconds : FadeSeconds;
            _fadeProgress = Outgoing == null ? 1f : 0f;
            _recent.Add(next.Id);
            if (_recent.Count > RecentMemory)
            {
                _recent.RemoveAt(0);
            }
        }

        private MusicTrack Pick(MusicContext context)
        {
            var fitting = _tracks.Where(t => t.Contexts.Contains(context)).ToList();
            var pool = fitting.Count > 0 ? fitting : _tracks.ToList();
            var fresh = pool.Where(t => !_recent.Contains(t.Id)).ToList();
            var choices = fresh.Count > 0 ? fresh : pool.Where(t => Current == null || t.Id != Current.Id).ToList();
            return choices.Count == 0 ? pool.FirstOrDefault() : choices[_random.NextInt(0, choices.Count)];
        }
    }
}
