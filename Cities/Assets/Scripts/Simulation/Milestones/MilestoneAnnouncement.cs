using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Milestones
{
    /// <summary>
    /// De melding bij een gehaalde bevolkingsmijlpaal (E16-05): titel, beloning en een lijst van wat er nieuw beschikbaar komt, gegroepeerd
    /// per soort (gebouwen, tools, wegen, voertuigen, beleid, ...). Zo ziet de speler niet alleen dat er iets gehaald is, maar ook wat hij er
    /// nu mee kan. De tekst komt uit de <see cref="UnlockTable"/>, dus een nieuwe regel verschijnt vanzelf in de melding.
    /// </summary>
    public sealed class MilestoneAnnouncement
    {
        private static readonly string[] CategoryOrder = { "building", "tool", "road", "vehicle", "policy", "tourist-site" };

        private static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>
        {
            ["building"] = "Gebouwen",
            ["tool"] = "Tools",
            ["road"] = "Wegen",
            ["vehicle"] = "Voertuigen",
            ["policy"] = "Beleid",
            ["tourist-site"] = "Toerisme",
        };

        private MilestoneAnnouncement(PopulationMilestone milestone, IReadOnlyList<AnnouncementGroup> groups)
        {
            Milestone = milestone;
            Groups = groups;
        }

        public PopulationMilestone Milestone { get; }

        /// <summary>Wat er nieuw beschikbaar komt, per soort; leeg als de mijlpaal niets vrijgeeft.</summary>
        public IReadOnlyList<AnnouncementGroup> Groups { get; }

        public int NewCount => Groups.Sum(g => g.Names.Count);

        public string Title => $"{Milestone.Title} bereikt";

        /// <summary>De uitleg voor de speler: bevolking en beloning, daarna per soort wat er nieuw is.</summary>
        public string Body
        {
            get
            {
                var lines = new List<string> { Milestone.Message };
                if (Groups.Count == 0)
                {
                    lines.Add("Er komt niets nieuws beschikbaar.");
                }
                else
                {
                    lines.Add("Nieuw beschikbaar:");
                    lines.AddRange(Groups.Select(g => $"{g.Category}: {string.Join(", ", g.Names)}"));
                }

                return string.Join("\n", lines);
            }
        }

        /// <param name="displayName">Weergavenaam van een definitie-ID; <c>null</c> of een lege uitkomst valt terug op een leesbare versie van het ID.</param>
        public static MilestoneAnnouncement Create(PopulationMilestone milestone, UnlockTable table, Func<string, string> displayName = null)
        {
            if (milestone is null)
            {
                throw new ArgumentNullException(nameof(milestone));
            }

            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var groups = table.DefinitionsFor(milestone.Id)
                .GroupBy(Kind)
                .OrderBy(g => Rank(g.Key))
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new AnnouncementGroup(
                    CategoryNames.TryGetValue(g.Key, out var category) ? category : Capitalize(g.Key),
                    g.Select(id => NameOf(id, displayName)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList()))
                .ToList();
            return new MilestoneAnnouncement(milestone, groups);
        }

        /// <summary>De melding voor elke nieuw gehaalde mijlpaal.</summary>
        public static IReadOnlyList<MilestoneAnnouncement> CreateAll(IEnumerable<MilestoneAchievement> fresh, UnlockTable table, Func<string, string> displayName = null)
        {
            if (fresh is null)
            {
                throw new ArgumentNullException(nameof(fresh));
            }

            return fresh.Select(a => Create(a.Milestone, table, displayName)).ToList();
        }

        private static string Kind(string definitionId)
        {
            var dot = definitionId.IndexOf('.');
            return dot < 0 ? definitionId : definitionId.Substring(0, dot);
        }

        private static int Rank(string kind)
        {
            var index = Array.IndexOf(CategoryOrder, kind);
            return index < 0 ? CategoryOrder.Length : index;
        }

        private static string NameOf(string id, Func<string, string> displayName)
        {
            var name = displayName?.Invoke(id);
            return string.IsNullOrWhiteSpace(name) ? Readable(id) : name;
        }

        // "tool.fire-station" wordt "Fire station": het deel na de soort is de naam.
        private static string Readable(string id)
        {
            var dot = id.IndexOf('.');
            return Capitalize((dot < 0 ? id : id.Substring(dot + 1)).Replace('.', ' ').Replace('-', ' '));
        }

        private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    /// <summary>Eén soort nieuwe definities in een mijlpaalmelding.</summary>
    public sealed class AnnouncementGroup
    {
        public AnnouncementGroup(string category, IReadOnlyList<string> names)
        {
            Category = category;
            Names = names;
        }

        public string Category { get; }

        public IReadOnlyList<string> Names { get; }
    }
}
