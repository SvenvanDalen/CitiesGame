using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Controls
{
    /// <summary>
    /// Nederlandse namen voor toetsen en muisknoppen waarvan het Input System een Engelse of afgekorte naam geeft.
    /// Letters, cijfers en leestekens houden de naam van het toetsenbord (die volgt de indeling van de speler).
    /// </summary>
    public static class KeyNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["<Keyboard>/upArrow"] = "Pijl omhoog",
            ["<Keyboard>/downArrow"] = "Pijl omlaag",
            ["<Keyboard>/leftArrow"] = "Pijl links",
            ["<Keyboard>/rightArrow"] = "Pijl rechts",
            ["<Keyboard>/space"] = "Spatie",
            ["<Keyboard>/enter"] = "Enter",
            ["<Keyboard>/tab"] = "Tab",
            ["<Keyboard>/backspace"] = "Backspace",
            ["<Keyboard>/delete"] = "Delete",
            ["<Keyboard>/insert"] = "Insert",
            ["<Keyboard>/home"] = "Home",
            ["<Keyboard>/end"] = "End",
            ["<Keyboard>/pageUp"] = "Page Up",
            ["<Keyboard>/pageDown"] = "Page Down",
            ["<Keyboard>/pause"] = "Pause",
            ["<Keyboard>/capsLock"] = "Caps Lock",
            ["<Keyboard>/leftShift"] = "Shift",
            ["<Keyboard>/rightShift"] = "Rechter Shift",
            ["<Keyboard>/leftCtrl"] = "Ctrl",
            ["<Keyboard>/rightCtrl"] = "Rechter Ctrl",
            ["<Keyboard>/leftAlt"] = "Alt",
            ["<Keyboard>/rightAlt"] = "Alt Gr",
            ["<Keyboard>/numpadPlus"] = "Numeriek +",
            ["<Keyboard>/numpadMinus"] = "Numeriek −",
            ["<Keyboard>/numpadMultiply"] = "Numeriek *",
            ["<Keyboard>/numpadDivide"] = "Numeriek /",
            ["<Keyboard>/numpadEnter"] = "Numeriek Enter",
            ["<Keyboard>/numpadPeriod"] = "Numeriek .",
            ["<Mouse>/rightButton"] = "Rechtermuisknop",
            ["<Mouse>/middleButton"] = "Middelste muisknop",
            ["<Mouse>/forwardButton"] = "Muisknop vooruit",
            ["<Mouse>/backButton"] = "Muisknop terug",
        };

        /// <summary>Nederlandse naam voor <paramref name="path"/>, anders <paramref name="fallback"/>.</summary>
        public static string For(string path, string fallback)
        {
            if (string.IsNullOrEmpty(path))
            {
                return fallback;
            }

            if (Names.TryGetValue(path, out var name))
            {
                return name;
            }

            const string numpad = "<Keyboard>/numpad";
            if (path.Length == numpad.Length + 1 && path.StartsWith(numpad, StringComparison.OrdinalIgnoreCase) && char.IsDigit(path[path.Length - 1]))
            {
                return "Numeriek " + path[path.Length - 1];
            }

            return fallback;
        }
    }
}
