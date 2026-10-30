using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Gameplay.Controls
{
    /// <summary>Welke bindingen de speler mag wijzigen en welke invoer daarbij niet als nieuwe toets telt.</summary>
    public static class BindingRules
    {
        /// <summary>Annuleert het wachten op een nieuwe toets; daarom zelf nooit toe te wijzen.</summary>
        public const string CancelPath = "<Keyboard>/escape";

        /// <summary>Selecteren en bouwen (<c>Tools/Apply</c>); blijft altijd op de linkermuisknop.</summary>
        public const string ApplyPath = "<Mouse>/leftButton";

        /// <summary>Apparaten waarvan toetsen en knoppen gekozen kunnen worden.</summary>
        public static readonly IReadOnlyList<string> AllowedDevices = new[] { "<Keyboard>", "<Mouse>" };

        /// <summary>
        /// Invoer die tijdens herbinden genegeerd wordt: muispositie en -beweging, het wiel, de linkermuisknop (selecteren
        /// en bouwen) en de verzamelcontrols die bij elke toets mee afgaan.
        /// </summary>
        public static readonly IReadOnlyList<string> ExcludedControls = new[]
        {
            "<Mouse>/position",
            "<Mouse>/delta",
            "<Mouse>/scroll",
            "<Mouse>/leftButton",
            "<Mouse>/press",
            "<Pointer>/press",
            "<Keyboard>/anyKey",
        };

        /// <summary>
        /// Herbindbaar zijn knoppen en de delen van composites (WASD, QE, ...). Assen en posities (muiswiel,
        /// muispositie) en de vaste menutoets niet.
        /// </summary>
        public static bool IsRebindable(BindingInfo binding)
        {
            switch (binding.Kind)
            {
                case BindingKind.Composite:
                    return false;
                case BindingKind.CompositePart:
                    return !IsReserved(binding.DefaultPath);
                default:
                    return binding.ActionIsButton && !IsReserved(binding.DefaultPath);
            }
        }

        /// <summary>Paden die altijd hun standaardfunctie houden.</summary>
        public static bool IsReserved(string path) => SamePath(path, CancelPath) || SamePath(path, ApplyPath);

        /// <summary>Of een pad als nieuwe toets gekozen mag worden.</summary>
        public static bool IsAssignable(string path)
        {
            if (string.IsNullOrEmpty(path) || IsReserved(path))
            {
                return false;
            }

            return !ExcludedControls.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                && AllowedDevices.Any(device => path.StartsWith(device + "/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Vergelijkt controlpaden zoals het Input System: hoofdletters tellen niet; leeg is gelijk aan leeg.</summary>
        public static bool SamePath(string a, string b) =>
            string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
