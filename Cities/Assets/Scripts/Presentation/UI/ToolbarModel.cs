using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Presentation.UI
{
    public enum ToolbarTab
    {
        Roads,
        Zones,
        Utilities,
        Services,
        Transit,
        Policies,
        Districts,
    }

    /// <summary>Eén knop in het subpaneel van een tab; <see cref="Id"/> is wat de koppeling met de tool of het paneel gebruikt.</summary>
    public readonly struct ToolbarEntry
    {
        public ToolbarEntry(string id, string label)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        public string Id { get; }

        public string Label { get; }
    }

    /// <summary>
    /// De hoofdtoolbalk (E18-04): zeven tabs met elk een subpaneel van knoppen. Het model houdt de gekozen tab en knop bij; de UI leest het en
    /// meldt klikken. Een tab opent zijn subpaneel, dezelfde tab nog eens sluit het weer.
    /// </summary>
    public sealed class ToolbarModel
    {
        private static readonly Dictionary<ToolbarTab, string> TabLabels = new Dictionary<ToolbarTab, string>
        {
            [ToolbarTab.Roads] = "Wegen",
            [ToolbarTab.Zones] = "Zones",
            [ToolbarTab.Utilities] = "Nutsvoorzieningen",
            [ToolbarTab.Services] = "Diensten",
            [ToolbarTab.Transit] = "OV",
            [ToolbarTab.Policies] = "Beleid",
            [ToolbarTab.Districts] = "Districten",
        };

        /// <summary>Alle tabs in toolbalkvolgorde.</summary>
        public static readonly IReadOnlyList<ToolbarTab> Tabs = new[]
        {
            ToolbarTab.Roads, ToolbarTab.Zones, ToolbarTab.Utilities, ToolbarTab.Services, ToolbarTab.Transit, ToolbarTab.Policies, ToolbarTab.Districts,
        };

        private readonly Dictionary<ToolbarTab, List<ToolbarEntry>> _entries = new Dictionary<ToolbarTab, List<ToolbarEntry>>();

        public ToolbarModel()
        {
            foreach (var tab in Tabs)
            {
                _entries[tab] = new List<ToolbarEntry>();
            }
        }

        public event Action Changed;

        /// <summary>De open tab, of <c>null</c> als het subpaneel dicht is.</summary>
        public ToolbarTab? OpenTab { get; private set; }

        /// <summary>Id van de gekozen knop, of <c>null</c>.</summary>
        public string SelectedId { get; private set; }

        public static string LabelOf(ToolbarTab tab) => TabLabels[tab];

        public IReadOnlyList<ToolbarEntry> EntriesOf(ToolbarTab tab) => _entries[tab];

        /// <summary>Voegt een knop toe aan een tab; een tweede met hetzelfde id wordt geweigerd.</summary>
        public void Add(ToolbarTab tab, ToolbarEntry entry)
        {
            if (_entries.Values.Any(list => list.Exists(e => e.Id == entry.Id)))
            {
                throw new ArgumentException($"Knop '{entry.Id}' bestaat al.", nameof(entry));
            }

            _entries[tab].Add(entry);
            Changed?.Invoke();
        }

        public void ToggleTab(ToolbarTab tab)
        {
            OpenTab = OpenTab == tab ? (ToolbarTab?)null : tab;
            Changed?.Invoke();
        }

        /// <summary>Kiest een knop van de open tab; een onbekend id, of een knop van een andere tab, verandert niets en geeft <c>false</c>.</summary>
        public bool Select(string id)
        {
            if (OpenTab is null || !_entries[OpenTab.Value].Exists(e => e.Id == id))
            {
                return false;
            }

            SelectedId = id;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Esc: eerst de keuze weg, daarna het subpaneel dicht.</summary>
        public void Cancel()
        {
            if (SelectedId != null)
            {
                SelectedId = null;
            }
            else
            {
                OpenTab = null;
            }

            Changed?.Invoke();
        }
    }
}
