using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Overlay voor gebouwen of wegstukken op basis van een verzamelfunctie en een versieteller.</summary>
    public sealed class EntityOverlaySource : IEntityOverlaySource
    {
        private readonly Action<List<OverlayEntityValue>> _collect;
        private readonly Func<long> _version;

        public EntityOverlaySource(string id, string name, OverlayTarget target, OverlayLegend legend, Action<List<OverlayEntityValue>> collect, Func<long> version = null)
        {
            if (target == OverlayTarget.Cell)
            {
                throw new ArgumentException("Een celoverlay gebruikt DelegateCellOverlaySource.", nameof(target));
            }

            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Target = target;
            Legend = legend ?? throw new ArgumentNullException(nameof(legend));
            _collect = collect ?? throw new ArgumentNullException(nameof(collect));
            _version = version;
        }

        public string Id { get; }

        public string Name { get; }

        public OverlayTarget Target { get; }

        public OverlayLegend Legend { get; }

        public long Version => _version?.Invoke() ?? 0;

        public void Collect(List<OverlayEntityValue> output)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            _collect(output);
        }
    }
}
