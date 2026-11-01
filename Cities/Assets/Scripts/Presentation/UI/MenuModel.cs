using System;
using System.Collections.Generic;

namespace Cities.Presentation.UI
{
    public enum MenuScreen
    {
        None,
        Main,
        Pause,
        NewCity,
        Load,
        Save,
    }

    /// <summary>
    /// De menu's (E18-05): hoofdmenu, pauzemenu, nieuwe stad, laden en opslaan als navigatie met een terugstapel. Het hoofdmenu hoort bij het
    /// opstarten, het pauzemenu bij een lopende stad; Esc opent het pauzemenu of gaat een scherm terug. Het model kent alleen de schermen,
    /// niet wat nieuwe stad, laden of opslaan doen; de UI koppelt daar de bestaande opslag en stadsstart aan.
    /// </summary>
    public sealed class MenuModel
    {
        private readonly Stack<MenuScreen> _stack = new Stack<MenuScreen>();

        public MenuModel(bool cityRunning = false)
        {
            CityRunning = cityRunning;
            if (!cityRunning)
            {
                _stack.Push(MenuScreen.Main);
            }
        }

        public event Action Changed;

        /// <summary>Of er een stad loopt; dan is het basisscherm het pauzemenu en kan er opgeslagen worden.</summary>
        public bool CityRunning { get; private set; }

        /// <summary>Het getoonde scherm; <see cref="MenuScreen.None"/> als er geen menu open is (stad speelt).</summary>
        public MenuScreen Current => _stack.Count == 0 ? MenuScreen.None : _stack.Peek();

        public bool IsOpen => _stack.Count > 0;

        /// <summary>Gaat naar een scherm; opslaan kan alleen bij een lopende stad. Geeft <c>false</c> als het niet mag.</summary>
        public bool Open(MenuScreen screen)
        {
            if (screen == MenuScreen.None || screen == Current || (screen == MenuScreen.Save && !CityRunning))
            {
                return false;
            }

            _stack.Push(screen);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Eén scherm terug. Het hoofdmenu zelf gaat niet weg; het pauzemenu wel (de stad hervat).</summary>
        public void Back()
        {
            if (_stack.Count > 0 && !(_stack.Count == 1 && Current == MenuScreen.Main))
            {
                _stack.Pop();
                Changed?.Invoke();
            }
        }

        /// <summary>Esc tijdens het spel: opent het pauzemenu, of gaat een scherm terug als er al een menu open is.</summary>
        public void Escape()
        {
            if (!IsOpen && CityRunning)
            {
                _stack.Push(MenuScreen.Pause);
                Changed?.Invoke();
            }
            else
            {
                Back();
            }
        }

        /// <summary>Een stad is gestart of geladen: alle menu's dicht en de stad loopt.</summary>
        public void StartCity()
        {
            _stack.Clear();
            CityRunning = true;
            Changed?.Invoke();
        }

        /// <summary>Terug naar het hoofdmenu (stad afsluiten).</summary>
        public void QuitToMain()
        {
            _stack.Clear();
            _stack.Push(MenuScreen.Main);
            CityRunning = false;
            Changed?.Invoke();
        }
    }
}
