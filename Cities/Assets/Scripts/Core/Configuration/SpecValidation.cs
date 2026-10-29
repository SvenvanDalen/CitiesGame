using System.Collections.Generic;

namespace Cities.Core.Configuration
{
    /// <summary>Korte controles voor spec-structs, met eenduidige meldingen.</summary>
    public static class SpecValidation
    {
        public static void Range(ICollection<string> errors, string field, float value, float min, float max, string unit = "")
        {
            if (float.IsNaN(value) || value < min || value > max)
            {
                errors.Add($"{field} moet tussen {min} en {max}{unit} liggen (is {value}{unit}).");
            }
        }

        public static void Range(ICollection<string> errors, string field, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                errors.Add($"{field} moet tussen {min} en {max} liggen (is {value}).");
            }
        }

        public static void NotNegative(ICollection<string> errors, string field, long value)
        {
            if (value < 0)
            {
                errors.Add($"{field} kan niet negatief zijn (is {value}).");
            }
        }
    }
}
