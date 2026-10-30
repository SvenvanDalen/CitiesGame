using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Presentation.UI;
using Cities.Simulation.Scheduling;
using UnityEngine.UIElements;

namespace Cities.Presentation.Debugging
{
    /// <summary>
    /// Debugpaneel: tijd per simulatiesysteem tegenover het budget uit E23-01, de tellers uit de
    /// <see cref="DebugRegistry"/> en schakelaars voor de overlays.
    /// </summary>
    public sealed class DebugPanel : CityPanel, IDisposable
    {
        private readonly DebugRegistry _registry;
        private readonly VisualElement _systems;
        private readonly Label _budget;
        private readonly VisualElement _counters;
        private readonly VisualElement _overlays;
        private readonly Dictionary<string, TimingRow> _timingRows = new Dictionary<string, TimingRow>(StringComparer.Ordinal);
        private readonly Dictionary<DebugCounter, Label> _counterValues = new Dictionary<DebugCounter, Label>();
        private bool _structureDirty = true;

        public DebugPanel(DebugRegistry registry)
            : base(DebugPanelText.Title)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            AddToClassList(UiClasses.DebugPanel);

            Add(Section(DebugPanelText.SectionTick));
            _budget = new Label { name = "budget" };
            _budget.AddToClassList(UiClasses.TextCaption);
            Add(_budget);
            _systems = new VisualElement { name = "systems" };
            Add(_systems);
            var hint = new Label(DebugPanelText.MeasureHint);
            hint.AddToClassList(UiClasses.TextCaption);
            hint.AddToClassList(UiClasses.TextMuted);
            Add(hint);

            Add(Section(DebugPanelText.SectionCounters));
            _counters = new VisualElement { name = "counters" };
            Add(_counters);

            Add(Section(DebugPanelText.SectionOverlays));
            _overlays = new VisualElement { name = "overlays" };
            Add(_overlays);

            _registry.Changed += MarkDirty;
        }

        /// <summary>Werkt alle waarden bij; bouwt tellers en overlay-schakelaars opnieuw op als de lijst veranderde.</summary>
        public void Refresh(SystemTimings timings, long population)
        {
            if (_structureDirty)
            {
                RebuildCountersAndOverlays();
                _structureDirty = false;
            }

            var budget = PerformanceBudgets.For((int)Math.Min(int.MaxValue, Math.Max(0L, population)));
            _budget.text = DebugPanelText.TickHeader(population, budget.TickMs);
            RefreshTimings(timings, budget.TickMs);
            foreach (var pair in _counterValues)
            {
                pair.Value.text = DebugPanelText.Count(pair.Key.Read());
            }
        }

        public void Dispose() => _registry.Changed -= MarkDirty;

        /// <summary>Tekst van een timingregel, voor tests.</summary>
        internal string TimingText(string system) => _timingRows.TryGetValue(system, out var row) ? row.Value.text : null;

        internal MeterColor TimingColor(string system) => _timingRows.TryGetValue(system, out var row) ? row.Meter.Color : default;

        private static Label Section(string title)
        {
            var label = new Label(title);
            label.AddToClassList(UiClasses.TextEmphasis);
            label.AddToClassList(UiClasses.SettingsSection);
            return label;
        }

        private void MarkDirty() => _structureDirty = true;

        private void RefreshTimings(SystemTimings timings, double tickBudgetMs)
        {
            if (timings is null || (timings.Systems.Count == 0 && timings.Tick.Samples == 0))
            {
                if (_timingRows.Count == 0 && _systems.childCount == 0)
                {
                    _systems.Add(new Label(DebugPanelText.NoSystems) { name = "empty" });
                }

                return;
            }

            _systems.Q("empty")?.RemoveFromHierarchy();
            UpdateRow(SystemTimings.TickName, timings.Tick, tickBudgetMs, tickBudgetMs);
            foreach (var system in timings.Systems)
            {
                // Elk systeem tegenover het hele tickbudget; zo is meteen te zien welk systeem het meeste kost.
                UpdateRow(system, timings.Get(system), tickBudgetMs, tickBudgetMs);
            }
        }

        private void UpdateRow(string name, TimingStats stats, double barBudgetMs, double colorBudgetMs)
        {
            if (!_timingRows.TryGetValue(name, out var row))
            {
                row = new TimingRow(name);
                _timingRows.Add(name, row);
                _systems.Add(row.Root);
            }

            row.Value.text = $"{DebugPanelText.Milliseconds(stats.AverageMs)} (max {DebugPanelText.Milliseconds(stats.MaxMs)})";
            row.Meter.Value = DebugPanelText.BudgetFraction(stats.AverageMs, barBudgetMs);
            row.Meter.Color = DebugPanelText.BudgetColor(stats.AverageMs, colorBudgetMs);
        }

        private void RebuildCountersAndOverlays()
        {
            _counters.Clear();
            _counterValues.Clear();
            string category = null;
            foreach (var counter in _registry.Counters)
            {
                if (counter.Category != category)
                {
                    category = counter.Category;
                    var heading = new Label(category);
                    heading.AddToClassList(UiClasses.TextCaption);
                    heading.AddToClassList(UiClasses.TextMuted);
                    _counters.Add(heading);
                }

                var row = new VisualElement();
                row.AddToClassList(UiClasses.DebugRow);
                var name = new Label(counter.Name);
                name.AddToClassList(UiClasses.DebugRowName);
                var value = new Label { name = "value" };
                value.AddToClassList(UiClasses.DebugRowValue);
                row.Add(name);
                row.Add(value);
                _counters.Add(row);
                _counterValues[counter] = value;
            }

            _overlays.Clear();
            foreach (var name in _registry.Overlays.Select(overlay => overlay.Name))
            {
                var toggle = new Toggle(name) { name = name, value = _registry.IsEnabled(name) };
                toggle.RegisterValueChangedCallback(change => _registry.SetEnabled(name, change.newValue));
                _overlays.Add(toggle);
            }
        }

        private sealed class TimingRow
        {
            public TimingRow(string name)
            {
                Root = new VisualElement { name = name };
                Root.AddToClassList(UiClasses.DebugTiming);
                var header = new VisualElement();
                header.AddToClassList(UiClasses.DebugRow);
                var label = new Label(name);
                label.AddToClassList(UiClasses.DebugRowName);
                Value = new Label();
                Value.AddToClassList(UiClasses.DebugRowValue);
                header.Add(label);
                header.Add(Value);
                Meter = new CityMeter();
                Root.Add(header);
                Root.Add(Meter);
            }

            public VisualElement Root { get; }

            public Label Value { get; }

            public CityMeter Meter { get; }
        }
    }
}
