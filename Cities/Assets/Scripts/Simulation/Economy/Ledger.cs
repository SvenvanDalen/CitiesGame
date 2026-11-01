using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Economy
{
    /// <summary>Waar geld vandaan komt of naartoe gaat, voor de boekhouding per categorie.</summary>
    public enum LedgerCategory
    {
        Taxes = 0,
        Trade = 1,
        Construction = 2,
        Upkeep = 3,
        Services = 4,
        Interest = 5,
        Other = 6,
    }

    /// <summary>
    /// Het geldmodel van de stad (E11-01): één saldo en een boekhouding per spelweek en per categorie, met inkomsten en uitgaven gescheiden.
    /// Een uitgave kan alleen als er genoeg saldo is (<see cref="TrySpend"/>); het saldo gaat niet onder nul, behalve door
    /// <see cref="Force"/> voor verplichte lasten als rente. De weken worden door de aanroeper geteld, zie <see cref="WeekOf"/>.
    /// </summary>
    public sealed class Ledger
    {
        /// <summary>Hoeveel weken de boekhouding bewaard wordt; oudere weken vallen weg.</summary>
        public const int HistoryWeeks = 104;

        public const int DaysPerWeek = 7;

        private static readonly int Categories = Enum.GetValues(typeof(LedgerCategory)).Length;

        private readonly Dictionary<int, long[]> _income = new Dictionary<int, long[]>();
        private readonly Dictionary<int, long[]> _expenses = new Dictionary<int, long[]>();

        public Ledger(long startBalance)
        {
            Balance = startBalance;
        }

        public long Balance { get; private set; }

        /// <summary>Spelweek (vanaf 0) bij een aantal verstreken dagen.</summary>
        public static int WeekOf(long daysElapsed) => (int)(Math.Max(0L, daysElapsed) / DaysPerWeek);

        /// <summary>Of het saldo een uitgave van <paramref name="amount"/> toelaat.</summary>
        public bool CanAfford(long amount) => amount >= 0 && Balance >= amount;

        /// <summary>Boekt een inkomst in <paramref name="week"/>.</summary>
        public void Earn(int week, LedgerCategory category, long amount)
        {
            CheckAmount(amount);
            Add(_income, week, category, amount);
            Balance += amount;
        }

        /// <summary>Boekt een uitgave als het saldo genoeg is; anders gebeurt er niets en is het resultaat <c>false</c>.</summary>
        public bool TrySpend(int week, LedgerCategory category, long amount)
        {
            CheckAmount(amount);
            if (Balance < amount)
            {
                return false;
            }

            Force(week, category, amount);
            return true;
        }

        /// <summary>Boekt een uitgave die niet kan worden geweigerd, ook als het saldo daarmee negatief wordt.</summary>
        public void Force(int week, LedgerCategory category, long amount)
        {
            CheckAmount(amount);
            Add(_expenses, week, category, amount);
            Balance -= amount;
        }

        public long Income(int week) => Sum(_income, week);

        public long Income(int week, LedgerCategory category) => Get(_income, week, category);

        public long Expenses(int week) => Sum(_expenses, week);

        public long Expenses(int week, LedgerCategory category) => Get(_expenses, week, category);

        /// <summary>Inkomsten min uitgaven van een week.</summary>
        public long Net(int week) => Income(week) - Expenses(week);

        private static void CheckAmount(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Een bedrag kan niet negatief zijn.");
            }
        }

        private static long Sum(Dictionary<int, long[]> book, int week)
        {
            var total = 0L;
            if (book.TryGetValue(week, out var row))
            {
                foreach (var value in row)
                {
                    total += value;
                }
            }

            return total;
        }

        private static long Get(Dictionary<int, long[]> book, int week, LedgerCategory category) =>
            book.TryGetValue(week, out var row) ? row[(int)category] : 0L;

        private void Add(Dictionary<int, long[]> book, int week, LedgerCategory category, long amount)
        {
            if (!book.TryGetValue(week, out var row))
            {
                row = new long[Categories];
                book[week] = row;
                Prune(week);
            }

            row[(int)category] += amount;
        }

        private void Prune(int newest)
        {
            foreach (var book in new[] { _income, _expenses })
            {
                foreach (var week in book.Keys.Where(w => w <= newest - HistoryWeeks).ToList())
                {
                    book.Remove(week);
                }
            }
        }
    }
}
