using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// De rampinstellingen van de speler (E15-13): rampen in zijn geheel aan of uit, en per ramptype aan of uit, hoe vaak het type optreedt
    /// (factor op <see cref="DisasterSpec.ChancePerDay"/>) en hoe zwaar de schade is (factor op de dagelijkse kosten en de schade aan gebouwen).
    /// Een type zonder eigen instelling gebruikt de standaard: aan, frequentie 1, schade 1.
    /// </summary>
    public sealed class DisasterSettings
    {
        /// <summary>Hoogste toegestane factor voor frequentie en schade.</summary>
        public const float MaxFactor = 5f;

        private readonly Dictionary<string, TypeSetting> _types = new Dictionary<string, TypeSetting>(StringComparer.Ordinal);

        /// <summary>Of rampen vanzelf mogen optreden; een handmatige trigger kan altijd.</summary>
        public bool RandomEnabled { get; set; } = true;

        /// <summary>De types met een eigen instelling, op naam gesorteerd (voor een stabiele save).</summary>
        public IEnumerable<string> Customised => _types.Keys.OrderBy(k => k, StringComparer.Ordinal);

        public bool IsEnabled(string name) => !_types.TryGetValue(Key(name), out var setting) || setting.Enabled;

        public float FrequencyFactor(string name) => _types.TryGetValue(Key(name), out var setting) ? setting.Frequency : 1f;

        public float DamageFactor(string name) => _types.TryGetValue(Key(name), out var setting) ? setting.Damage : 1f;

        public void SetEnabled(string name, bool enabled) => Update(name, s => s.Enabled = enabled);

        /// <summary>Hoe vaak het type optreedt: 0 nooit, 1 normaal, tot <see cref="MaxFactor"/> keer zo vaak.</summary>
        public void SetFrequencyFactor(string name, float factor)
        {
            Check(factor, nameof(factor));
            Update(name, s => s.Frequency = factor);
        }

        /// <summary>Hoe zwaar de schade is: 0 geen schade, 1 normaal, tot <see cref="MaxFactor"/> keer zo veel.</summary>
        public void SetDamageFactor(string name, float factor)
        {
            Check(factor, nameof(factor));
            Update(name, s => s.Damage = factor);
        }

        /// <summary>Kans per dag dat het type optreedt, met de frequentiefactor erin en begrensd op 1.</summary>
        public float ChancePerDay(DisasterSpec spec)
        {
            if (spec is null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Math.Min(1f, spec.ChancePerDay * FrequencyFactor(spec.Name));
        }

        /// <summary>Kosten van een dag, geschaald met de schadefactor van het type en afgerond op hele munteenheden.</summary>
        public long ScaledCost(string name, long cost) =>
            (long)Math.Round(cost * (double)DamageFactor(name), MidpointRounding.AwayFromZero);

        /// <summary>Zet alles terug naar de standaard.</summary>
        public void Reset()
        {
            RandomEnabled = true;
            _types.Clear();
        }

        internal void Restore(bool randomEnabled, IEnumerable<(string Name, bool Enabled, float Frequency, float Damage)> types)
        {
            Reset();
            RandomEnabled = randomEnabled;
            foreach (var (name, enabled, frequency, damage) in types)
            {
                _types[name] = new TypeSetting { Enabled = enabled, Frequency = frequency, Damage = damage };
            }
        }

        private static string Key(string name) =>
            string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Een ramptype heeft een naam nodig.", nameof(name)) : name;

        private static void Check(float factor, string parameter)
        {
            if (!(factor >= 0f && factor <= MaxFactor))
            {
                throw new ArgumentOutOfRangeException(parameter, factor, $"De factor ligt tussen 0 en {MaxFactor}.");
            }
        }

        private void Update(string name, Action<TypeSetting> change)
        {
            var key = Key(name);
            var setting = _types.TryGetValue(key, out var existing) ? existing : new TypeSetting { Enabled = true, Frequency = 1f, Damage = 1f };
            change(setting);
            _types[key] = setting;
        }

        // Klasse zodat Update de waarden kan wijzigen zonder een kopie te maken.
        private sealed class TypeSetting
        {
            public bool Enabled { get; set; }

            public float Frequency { get; set; }

            public float Damage { get; set; }
        }
    }
}
