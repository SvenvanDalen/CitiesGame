using System;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Wat het menu de volgende start van de hoofdscène meegeeft (E26-02): een seed, een moeilijkheid of een save om te laden. Het menu laadt
    /// de scène opnieuw; de systemen halen de aanvraag bij hun start op. Een aanvraag geldt één keer (<see cref="Take"/>).
    /// </summary>
    public sealed class StartRequest
    {
        private static StartRequest _pending;

        public ulong? Seed { get; set; }

        /// <summary>Waarde van <c>Difficulty</c>: 0 makkelijk, 1 normaal, 2 moeilijk.</summary>
        public int Difficulty { get; set; } = 1;

        /// <summary>Naam van de save die na de start geladen wordt; <c>null</c> voor een nieuwe stad.</summary>
        public string LoadSlot { get; set; }

        /// <summary>De stad start meteen zonder hoofdmenu.</summary>
        public bool SkipMenu { get; set; } = true;

        /// <summary>De nog niet opgehaalde aanvraag, of <c>null</c>.</summary>
        public static StartRequest Pending => _pending;

        public static void Submit(StartRequest request) => _pending = request ?? throw new ArgumentNullException(nameof(request));

        /// <summary>De huidige aanvraag, of <c>null</c>; wist hem daarna.</summary>
        public static StartRequest Take()
        {
            var request = _pending;
            _pending = null;
            return request;
        }

        /// <summary>De moeilijkheid die de laatste aanvraag koos; blijft staan voor de rest van de sessie.</summary>
        public static int ChosenDifficulty { get; private set; } = 1;

        /// <summary>Onthoudt de gekozen moeilijkheid voor de systemen die hem na het opstarten lezen.</summary>
        public static void RememberDifficulty(int difficulty) => ChosenDifficulty = Math.Max(0, Math.Min(2, difficulty));

        /// <summary>Wist alles, voor tests.</summary>
        public static void Reset()
        {
            _pending = null;
            ChosenDifficulty = 1;
        }
    }
}
