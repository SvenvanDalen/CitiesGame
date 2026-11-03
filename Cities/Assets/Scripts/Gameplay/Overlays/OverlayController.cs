using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Register van overlay-bronnen met hoogstens één actieve overlay. De weergave luistert naar <see cref="Changed"/> en
    /// bakt de actieve bron; <see cref="NeedsRefresh"/> meldt ook als een actieve bron nieuwe gegevens heeft.
    /// </summary>
    public sealed class OverlayController
    {
        private readonly List<IOverlaySource> _sources = new List<IOverlaySource>();
        private long _shownVersion = -1;

        public event Action Changed;

        public IReadOnlyList<IOverlaySource> Sources => _sources;

        /// <summary>De actieve bron, of <c>null</c> als er geen overlay aan staat.</summary>
        public IOverlaySource Active { get; private set; }

        public IOverlaySource Find(string id)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (string.Equals(_sources[i].Id, id, StringComparison.Ordinal))
                {
                    return _sources[i];
                }
            }

            return null;
        }

        public void Register(IOverlaySource source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (Find(source.Id) != null)
            {
                throw new ArgumentException($"Er is al een overlay '{source.Id}'.", nameof(source));
            }

            _sources.Add(source);
            Changed?.Invoke();
        }

        public bool Unregister(string id)
        {
            var source = Find(id);
            if (source == null)
            {
                return false;
            }

            _sources.Remove(source);
            if (ReferenceEquals(Active, source))
            {
                Active = null;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Zet een overlay aan; een onbekende sleutel doet niets en geeft <c>false</c>.</summary>
        public bool Activate(string id)
        {
            var source = Find(id);
            if (source == null)
            {
                return false;
            }

            if (!ReferenceEquals(Active, source))
            {
                Active = source;
                _shownVersion = -1;
                Changed?.Invoke();
            }

            return true;
        }

        public void Deactivate()
        {
            if (Active == null)
            {
                return;
            }

            Active = null;
            Changed?.Invoke();
        }

        /// <summary>Zet de overlay aan als hij uit staat en uit als hij al actief is.</summary>
        public void Toggle(string id)
        {
            if (Active != null && string.Equals(Active.Id, id, StringComparison.Ordinal))
            {
                Deactivate();
            }
            else
            {
                Activate(id);
            }
        }

        /// <summary>Volgende overlay in de lijst, na de laatste weer uit; vanuit uit de eerste.</summary>
        public void Cycle()
        {
            if (_sources.Count == 0)
            {
                return;
            }

            var next = Active == null ? 0 : _sources.IndexOf(Active) + 1;
            if (next >= _sources.Count)
            {
                Deactivate();
            }
            else
            {
                Activate(_sources[next].Id);
            }
        }

        /// <summary>Of de actieve bron veranderde sinds de laatste <see cref="MarkShown"/>.</summary>
        public bool NeedsRefresh => Active != null && Active.Version != _shownVersion;

        /// <summary>De weergave heeft de huidige versie van de actieve bron in gang gezet.</summary>
        public void MarkShown()
        {
            if (Active != null)
            {
                _shownVersion = Active.Version;
            }
        }
    }
}
