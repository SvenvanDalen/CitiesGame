using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cities.Core.Settings
{
    public enum SettingKind : byte
    {
        Toggle,
        Slider,
        Choice,
    }

    /// <summary>Eén instelling (E18-07): sleutel, categorie, soort, bereik en standaardwaarde. Het instellingenpaneel wordt hieruit opgebouwd.</summary>
    public sealed class SettingDefinition
    {
#pragma warning disable S107 // Gegevensklasse: elke parameter is een eigen eigenschap
        public SettingDefinition(string key, string category, string label, SettingKind kind, float defaultValue, float min = 0f, float max = 1f, IReadOnlyList<string> choices = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Een instelling heeft een sleutel nodig.", nameof(key));
            }

            if (kind == SettingKind.Toggle)
            {
                min = 0f;
                max = 1f;
            }

            if (kind == SettingKind.Choice)
            {
                if (choices == null || choices.Count < 2)
                {
                    throw new ArgumentException("Een keuze heeft minstens twee opties.", nameof(choices));
                }

                min = 0f;
                max = choices.Count - 1;
            }

            if (!(min < max) || !(defaultValue >= min && defaultValue <= max))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue, $"De standaardwaarde ligt niet in {min} tot {max}.");
            }

            Key = key;
            Category = category;
            Label = label;
            Kind = kind;
            Default = defaultValue;
            Min = min;
            Max = max;
            Choices = choices ?? Array.Empty<string>();
        }
#pragma warning restore S107

        public string Key { get; }

        public string Category { get; }

        public string Label { get; }

        public SettingKind Kind { get; }

        public float Default { get; }

        public float Min { get; }

        public float Max { get; }

        public IReadOnlyList<string> Choices { get; }

        /// <summary>Houdt een waarde binnen het bereik; toggles en keuzes worden afgerond op een heel getal.</summary>
        public float Clamp(float value)
        {
            if (float.IsNaN(value))
            {
                return Default;
            }

            var clamped = Math.Max(Min, Math.Min(Max, value));
            return Kind == SettingKind.Slider ? clamped : (float)Math.Round(clamped, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>De standaard instellingen per categorie (E18-07): beeld, geluid, besturing en gameplay.</summary>
    public static class SettingsCatalog
    {
        public const string Display = "Beeld";
        public const string Audio = "Geluid";
        public const string Controls = "Besturing";
        public const string Gameplay = "Gameplay";

        public const string Quality = "display.quality";
        public const string UiScaleAuto = "display.ui-scale-auto";
        public const string UiScale = "display.ui-scale";
        public const string Fullscreen = "display.fullscreen";
        public const string VSync = "display.vsync";
        public const string VolumeMaster = "audio.master";
        public const string VolumeMusic = "audio.music";
        public const string VolumeAmbient = "audio.ambient";
        public const string VolumeEffects = "audio.effects";
        public const string VolumeUi = "audio.ui";
        public const string EdgeScroll = "controls.edge-scroll";
        public const string CameraSensitivity = "controls.camera-sensitivity";
        public const string InvertZoom = "controls.invert-zoom";
        public const string AutosaveMinutes = "gameplay.autosave-minutes";
        public const string Tooltips = "gameplay.tooltips";
        public const string CitizenMessages = "gameplay.citizen-messages";
        public const string DisasterNotices = "gameplay.disaster-notices";

        public static IReadOnlyList<SettingDefinition> All { get; } = new[]
        {
            new SettingDefinition(Quality, Display, "Grafische kwaliteit", SettingKind.Choice, 2f, choices: new[] { "Laag", "Middel", "Hoog", "Aangepast" }),
            new SettingDefinition(UiScaleAuto, Display, "Interfaceschaal automatisch", SettingKind.Toggle, 1f),
            new SettingDefinition(UiScale, Display, "Interfaceschaal", SettingKind.Slider, 1f, 0.5f, 3f),
            new SettingDefinition(Fullscreen, Display, "Volledig scherm", SettingKind.Toggle, 1f),
            new SettingDefinition(VSync, Display, "Verticale synchronisatie", SettingKind.Toggle, 1f),
            new SettingDefinition(VolumeMaster, Audio, "Hoofdvolume", SettingKind.Slider, 0.8f),
            new SettingDefinition(VolumeMusic, Audio, "Muziek", SettingKind.Slider, 0.6f),
            new SettingDefinition(VolumeAmbient, Audio, "Omgeving", SettingKind.Slider, 0.8f),
            new SettingDefinition(VolumeEffects, Audio, "Effecten", SettingKind.Slider, 0.8f),
            new SettingDefinition(VolumeUi, Audio, "Interface", SettingKind.Slider, 0.8f),
            new SettingDefinition(EdgeScroll, Controls, "Scrollen aan de schermrand", SettingKind.Toggle, 0f),
            new SettingDefinition(CameraSensitivity, Controls, "Cameragevoeligheid", SettingKind.Slider, 1f, 0.2f, 3f),
            new SettingDefinition(InvertZoom, Controls, "Zoomrichting omkeren", SettingKind.Toggle, 0f),
            new SettingDefinition(AutosaveMinutes, Gameplay, "Automatisch opslaan (minuten)", SettingKind.Slider, 10f, 1f, 60f),
            new SettingDefinition(Tooltips, Gameplay, "Tooltips tonen", SettingKind.Toggle, 1f),
            new SettingDefinition(CitizenMessages, Gameplay, "Berichten van burgers", SettingKind.Toggle, 0f),
            new SettingDefinition(DisasterNotices, Gameplay, "Rampmeldingen", SettingKind.Toggle, 1f),
        };
    }

    /// <summary>
    /// De instellingen van de speler (E22-05, E18-07): waarden per sleutel, begrensd door hun <see cref="SettingDefinition"/>. Ze gaan als
    /// vlak JSON-object in een <see cref="ISettingsStore"/> onder <see cref="StoreName"/>, dus per gebruiker en los van een savegame. Een
    /// ontbrekend, kapot of onbekend deel van het bestand valt terug op de standaard.
    /// </summary>
    public sealed class UserSettings
    {
        public const string StoreName = "user";

        private readonly Dictionary<string, SettingDefinition> _definitions;
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

        public UserSettings()
            : this(SettingsCatalog.All)
        {
        }

        public UserSettings(IReadOnlyList<SettingDefinition> definitions)
        {
            if (definitions is null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = definitions.ToDictionary(d => d.Key, StringComparer.Ordinal);
            Definitions = definitions;
        }

        public IReadOnlyList<SettingDefinition> Definitions { get; }

        /// <summary>Een instelling kreeg een nieuwe waarde.</summary>
        public event Action<string> Changed;

        public IEnumerable<string> Categories => Definitions.Select(d => d.Category).Distinct();

        public IEnumerable<SettingDefinition> InCategory(string category) => Definitions.Where(d => d.Category == category);

        public float Get(string key) => _values.TryGetValue(key, out var value) ? value : Definition(key).Default;

        public bool GetBool(string key) => Get(key) >= 0.5f;

        public int GetChoice(string key) => (int)Math.Round(Get(key), MidpointRounding.AwayFromZero);

        /// <summary>Zet een waarde (begrensd) en meldt het als hij veranderde; geeft de uiteindelijke waarde.</summary>
        public float Set(string key, float value)
        {
            var clamped = Definition(key).Clamp(value);
            if (CompareTo(Get(key), clamped) != 0)
            {
                _values[key] = clamped;
                Changed?.Invoke(key);
            }

            return clamped;
        }

        public void SetBool(string key, bool value) => Set(key, value ? 1f : 0f);

        public void Reset(string key)
        {
            if (_values.Remove(Definition(key).Key))
            {
                Changed?.Invoke(key);
            }
        }

        public void ResetAll()
        {
            foreach (var key in _values.Keys.ToList())
            {
                Reset(key);
            }
        }

        /// <summary>Een vlak JSON-object met alleen de afwijkingen van de standaard, op sleutel gesorteerd.</summary>
        public string ToJson()
        {
            var text = new StringBuilder("{");
            var first = true;
            foreach (var pair in _values.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append(first ? "\n  " : ",\n  ").Append('"').Append(pair.Key).Append("\": ")
                    .Append(pair.Value.ToString("R", CultureInfo.InvariantCulture));
                first = false;
            }

            return text.Append(first ? "}" : "\n}").ToString();
        }

        /// <summary>Leest waarden uit JSON van <see cref="ToJson"/>; geeft het aantal overgenomen instellingen. Onbekende of ongeldige regels worden overgeslagen.</summary>
        public int LoadJson(string json)
        {
            var loaded = new List<KeyValuePair<string, float>>();
            foreach (var line in (json ?? string.Empty).Split('\n'))
            {
                var parts = line.Trim().TrimEnd(',').Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim().Trim('"');
                if (_definitions.ContainsKey(key) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    && !float.IsInfinity(value))
                {
                    loaded.Add(new KeyValuePair<string, float>(key, value));
                }
            }

            foreach (var pair in loaded)
            {
                Set(pair.Key, pair.Value);
            }

            return loaded.Count;
        }

        public void Save(ISettingsStore store)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            store.Write(StoreName, ToJson());
        }

        /// <summary>Leest de opgeslagen instellingen; zonder bestand blijft alles standaard. Geeft het aantal overgenomen instellingen.</summary>
        public int Load(ISettingsStore store)
        {
            if (store is null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            return store.TryRead(StoreName, out var contents) ? LoadJson(contents) : 0;
        }

        private static int CompareTo(float a, float b) => a.CompareTo(b);

        private SettingDefinition Definition(string key) =>
            key != null && _definitions.TryGetValue(key, out var definition) ? definition : throw new ArgumentException($"Onbekende instelling '{key}'.", nameof(key));
    }

    /// <summary>
    /// Interfaceschaal (E18-08): automatisch uit de schermhoogte en de DPI van het scherm, of handmatig. 1080p is schaal 1; 4K krijgt 2.
    /// Een ultrawide scherm schaalt op de hoogte, zodat de breedte de interface niet laat groeien.
    /// </summary>
    public static class UiScaling
    {
        public const float ReferenceHeight = 1080f;
        public const float MinScale = 0.5f;
        public const float MaxScale = 3f;

        /// <summary>De schaal voor een scherm van <paramref name="height"/> pixels; een DPI boven 96 telt mee (tot 1,5) als het scherm lager dan 1440p is.</summary>
        public static float Automatic(int width, int height, float dpi)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Een scherm heeft een formaat.");
            }

            var byHeight = height / ReferenceHeight;
            var byDpi = dpi > 96f && height < 1440 ? Math.Min(dpi / 96f, 1.5f) : 0f;
            return Clamp(Math.Max(byHeight, byDpi));
        }

        /// <summary>De schaal die nu geldt: de handmatige instelling of de automatische.</summary>
        public static float Effective(UserSettings settings, int width, int height, float dpi)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return settings.GetBool(SettingsCatalog.UiScaleAuto) ? Automatic(width, height, dpi) : Clamp(settings.Get(SettingsCatalog.UiScale));
        }

        /// <summary>Is het scherm ultrawide (breder dan 21:9 min een beetje)?</summary>
        public static bool IsUltrawide(int width, int height) => height > 0 && width / (float)height >= 2.3f;

        private static float Clamp(float scale) => Math.Max(MinScale, Math.Min(MaxScale, scale));
    }
}
