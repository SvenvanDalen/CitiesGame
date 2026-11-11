using System;
using System.Collections.Generic;

namespace Cities.Gameplay.UI
{
    /// <summary>
    /// Helpsysteem (E18-10): korte uitleg per tool. Een hint verschijnt de eerste keer dat een tool wordt gekozen, daarna alleen op
    /// verzoek; hints kunnen worden uitgezet.
    /// </summary>
    public sealed class ToolHints
    {
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();
        private readonly HashSet<string> _seen = new HashSet<string>();

        public bool Enabled { get; set; } = true;

        public void Register(string tool, string text)
        {
            if (string.IsNullOrWhiteSpace(tool) || string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Een hint heeft een tool en een tekst nodig.");
            }

            _texts[tool] = text;
        }

        /// <summary>De tekst voor een tool die net is gekozen: alleen de eerste keer, anders null.</summary>
        public string OnToolChosen(string tool)
        {
            if (!Enabled || !_texts.TryGetValue(tool ?? string.Empty, out var text) || !_seen.Add(tool))
            {
                return null;
            }

            return text;
        }

        /// <summary>De tekst op verzoek (de helpknop), ook als ze al is gezien.</summary>
        public string Explain(string tool) => _texts.TryGetValue(tool ?? string.Empty, out var text) ? text : null;

        public void ResetSeen() => _seen.Clear();
    }
}
