using System;

namespace Cities.Simulation.Economy
{
    /// <summary>Wat er gebeurt als de stad te lang rood staat.</summary>
    public enum DebtMode
    {
        /// <summary>Na <see cref="Loans.BankruptAfterWeeks"/> weken met een negatief saldo is de stad failliet.</summary>
        Bankruptcy = 0,

        /// <summary>Cheat-modus: geen faillissement en geen leenlimiet.</summary>
        Cheat = 1,
    }

    /// <summary>
    /// Leningen van de stad (E11-06). Lenen kan tot een limiet, de schuld kost wekelijks rente (<see cref="WeeklyRate"/>) en lost wekelijks
    /// een vast deel af (<see cref="MinimumRepayment"/>), als het saldo dat toelaat. Rente is verplicht en kan het saldo negatief maken;
    /// blijft dat <see cref="BankruptAfterWeeks"/> weken zo, dan is de stad failliet (tenzij <see cref="DebtMode.Cheat"/>).
    /// Alle boekingen lopen via het <see cref="Ledger"/>.
    /// </summary>
    public sealed class Loans
    {
        /// <summary>Rente per week over de openstaande schuld (ruim 7% per jaar).</summary>
        public const double WeeklyRate = 0.0014;

        /// <summary>Deel van de schuld dat elke week verplicht wordt afgelost.</summary>
        public const double MinimumRepayment = 0.01;

        public const int BankruptAfterWeeks = 8;

        private readonly Ledger _ledger;
        private int _weeksInRed;

        public Loans(Ledger ledger, long limit, DebtMode mode = DebtMode.Bankruptcy)
        {
            if (limit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "De leenlimiet kan niet negatief zijn.");
            }

            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            Limit = limit;
            Mode = mode;
        }

        public long Limit { get; }

        public DebtMode Mode { get; }

        /// <summary>Openstaande schuld.</summary>
        public long Debt { get; private set; }

        public bool IsBankrupt { get; private set; }

        /// <summary>Leent <paramref name="amount"/>; mislukt (<c>false</c>) boven de limiet, behalve in cheat-modus.</summary>
        public bool Borrow(int week, long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Een bedrag kan niet negatief zijn.");
            }

            if (IsBankrupt || (Mode == DebtMode.Bankruptcy && Debt + amount > Limit))
            {
                return false;
            }

            _ledger.Earn(week, LedgerCategory.Other, amount);
            Debt += amount;
            return true;
        }

        /// <summary>Lost tot <paramref name="amount"/> af, begrensd door de schuld en het saldo; geeft terug hoeveel er is afgelost.</summary>
        public long Repay(int week, long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Een bedrag kan niet negatief zijn.");
            }

            var paid = Math.Min(Math.Min(amount, Debt), Math.Max(0L, _ledger.Balance));
            if (paid > 0)
            {
                _ledger.Force(week, LedgerCategory.Other, paid);
                Debt -= paid;
            }

            return paid;
        }

        /// <summary>Eén spelweek: rente (verplicht), verplichte aflossing en de controle op faillissement.</summary>
        public void EndWeek(int week)
        {
            if (Debt > 0)
            {
                _ledger.Force(week, LedgerCategory.Interest, (long)Math.Ceiling(Debt * WeeklyRate));
                Repay(week, (long)Math.Ceiling(Debt * MinimumRepayment));
            }

            _weeksInRed = _ledger.Balance < 0 ? _weeksInRed + 1 : 0;
            IsBankrupt |= Mode == DebtMode.Bankruptcy && _weeksInRed >= BankruptAfterWeeks;
        }
    }
}
