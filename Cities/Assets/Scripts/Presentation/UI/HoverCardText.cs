using System.Collections.Generic;
using System.Text;
using Cities.Gameplay.Tools;

namespace Cities.Presentation.UI
{
    /// <summary>De tekst van de infokaart bij de cursor (E27-10): titel, cijfers en daaronder de problemen.</summary>
    public static class HoverCardText
    {
        public const string ProblemMark = "! ";

        public static string Format(InspectionReport report)
        {
            var text = new StringBuilder(report.Title);
            Append(text, report.Details, string.Empty);
            Append(text, report.Problems, ProblemMark);
            return text.ToString();
        }

        private static void Append(StringBuilder text, IReadOnlyList<string> lines, string prefix)
        {
            foreach (var line in lines)
            {
                text.Append('\n').Append(prefix).Append(line);
            }
        }
    }
}
