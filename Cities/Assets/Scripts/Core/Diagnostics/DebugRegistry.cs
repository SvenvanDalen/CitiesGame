using System;
using System.Collections.Generic;

namespace Cities.Core.Diagnostics
{
    /// <summary>Tekent wereldgeometrie voor één overlay (cellenraster, lanegraaf, ID's, ...).</summary>
    public interface IDebugOverlay
    {
        /// <summary>Korte, unieke naam, bijvoorbeeld <c>Cellen</c>.</summary>
        string Name { get; }

        void Collect(DebugGeometry geometry, in DebugView view);
    }

    /// <summary>Getal dat het debugpaneel toont, bijvoorbeeld het aantal burgers of openstaande commands.</summary>
    public readonly struct DebugCounter
    {
        public DebugCounter(string category, string name, Func<long> read)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public string Category { get; }

        public string Name { get; }

        public Func<long> Read { get; }
    }

    /// <summary>
    /// Centrale lijst van tellers en overlays. Systemen melden zich aan bij het starten en af bij het opruimen; het
    /// debugpaneel en de overlay-renderer lezen de lijst. Alles op de main thread.
    /// </summary>
    public sealed class DebugRegistry
    {
        private readonly List<DebugCounter> _counters = new List<DebugCounter>();
        private readonly List<IDebugOverlay> _overlays = new List<IDebugOverlay>();
        private readonly HashSet<string> _enabled = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>De gedeelde lijst van het spel.</summary>
        public static DebugRegistry Shared { get; } = new DebugRegistry();

        /// <summary>Na elke aan- of afmelding en elke wisseling van een overlay.</summary>
        public event Action Changed;

        public IReadOnlyList<DebugCounter> Counters => _counters;

        public IReadOnlyList<IDebugOverlay> Overlays => _overlays;

        /// <summary>Meldt een teller aan; geeft een token terug dat hem weer afmeldt.</summary>
        public IDisposable AddCounter(string category, string name, Func<long> read)
        {
            var counter = new DebugCounter(category, name, read);
            _counters.Add(counter);
            Changed?.Invoke();
            return new Registration(() => Remove(counter));
        }

        /// <summary>Meldt een overlay aan (standaard uit); geeft een token terug dat hem weer afmeldt.</summary>
        public IDisposable AddOverlay(IDebugOverlay overlay)
        {
            if (overlay is null)
            {
                throw new ArgumentNullException(nameof(overlay));
            }

            if (Find(overlay.Name) != null)
            {
                throw new ArgumentException($"Er is al een overlay '{overlay.Name}'.", nameof(overlay));
            }

            _overlays.Add(overlay);
            Changed?.Invoke();
            return new Registration(() => Remove(overlay));
        }

        public IDebugOverlay Find(string name) => _overlays.Find(o => o.Name == name);

        public bool IsEnabled(string name) => _enabled.Contains(name);

        public void SetEnabled(string name, bool enabled)
        {
            var changed = enabled ? _enabled.Add(name) : _enabled.Remove(name);
            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Toggle(string name) => SetEnabled(name, !IsEnabled(name));

        /// <summary>Laat alle aangezette overlays tekenen; een fout in één overlay stopt de andere niet.</summary>
        public void Collect(DebugGeometry geometry, in DebugView view, Action<IDebugOverlay, Exception> onError = null)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            geometry.Clear();
            foreach (var overlay in _overlays)
            {
                if (!_enabled.Contains(overlay.Name))
                {
                    continue;
                }

                try
                {
                    overlay.Collect(geometry, view);
                }
                catch (Exception exception) when (onError != null)
                {
                    onError(overlay, exception);
                }
            }
        }

        private void Remove(DebugCounter counter)
        {
            var index = _counters.FindIndex(c => ReferenceEquals(c.Read, counter.Read) && c.Name == counter.Name);
            if (index >= 0)
            {
                _counters.RemoveAt(index);
                Changed?.Invoke();
            }
        }

        private void Remove(IDebugOverlay overlay)
        {
            if (_overlays.Remove(overlay))
            {
                Changed?.Invoke();
            }
        }

        private sealed class Registration : IDisposable
        {
            private Action _remove;

            public Registration(Action remove) => _remove = remove;

            public void Dispose()
            {
                _remove?.Invoke();
                _remove = null;
            }
        }
    }
}
