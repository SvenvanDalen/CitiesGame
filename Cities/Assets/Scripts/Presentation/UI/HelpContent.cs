using System.Collections.Generic;

namespace Cities.Presentation.UI
{
    /// <summary>Tekst van het hulpscherm (E27-16): alle sneltoetsen, afgeleid van <see cref="ToolbarHotkeys"/> zodat ze nooit afwijken.</summary>
    public static class HelpContent
    {
        public static IReadOnlyList<string> Lines()
        {
            var lines = new List<string> { "Gereedschap" };
            foreach (var tab in ToolbarModel.Tabs)
            {
                lines.Add($"{ToolbarHotkeys.KeyOf(tab)}  {ToolbarModel.LabelOf(tab)}");
            }

            lines.Add("1 tot 9  kies een gereedschap in het open paneel");
            lines.Add("T  draai");
            lines.Add("Rechtsklik of Esc  stop het gereedschap");
            lines.Add("Ctrl+Z / Ctrl+Y  ongedaan maken / opnieuw");
            lines.Add("F1  dit scherm");
            return lines;
        }
    }
}
