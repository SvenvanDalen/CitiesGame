using System.Collections.Generic;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Bepaalt in welke categorie van de werkbalk (E27-01) een plaatsingstool hoort, op basis van zijn naam. Zo hoeft de scène niet te weten
    /// welke tab een voorziening heeft; wat niet in de lijst staat komt bij de diensten.
    /// </summary>
    public static class ToolbarCatalog
    {
        private static readonly Dictionary<string, ToolbarTab> ByName = new Dictionary<string, ToolbarTab>
        {
            ["Centrale"] = ToolbarTab.Utilities,
            ["Waterpomp"] = ToolbarTab.Utilities,
            ["Rioolzuivering"] = ToolbarTab.Utilities,
            ["Stortplaats"] = ToolbarTab.Utilities,
            ["Brandweer"] = ToolbarTab.Services,
            ["Politie"] = ToolbarTab.Services,
            ["Kliniek"] = ToolbarTab.Services,
            ["School"] = ToolbarTab.Services,
            ["Bushalte"] = ToolbarTab.Transit,
            ["Boerderij"] = ToolbarTab.Industry,
            ["Houtkap"] = ToolbarTab.Industry,
            ["Mijn"] = ToolbarTab.Industry,
            ["Oliewinning"] = ToolbarTab.Industry,
            ["Trekpleister"] = ToolbarTab.Industry,
            ["Hotel"] = ToolbarTab.Industry,
        };

        /// <summary>De tab voor een plaatsingstool met deze naam.</summary>
        public static ToolbarTab TabFor(string toolName)
            => toolName != null && ByName.TryGetValue(toolName, out var tab) ? tab : ToolbarTab.Services;
    }
}
