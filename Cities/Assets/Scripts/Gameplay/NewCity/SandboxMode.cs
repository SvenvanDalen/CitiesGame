using System;
using Cities.Simulation.Economy;
using Cities.World.Tiles;

namespace Cities.Gameplay.NewCity
{
    /// <summary>
    /// Zet de zandbak-schakelaars van <see cref="DifficultySettings"/> (E22-04) door op de systemen: onbeperkt geld op het <see cref="Ledger"/>
    /// en alle kaarttegels ontgrendeld. Gebouwen, wegen en beleid vragen <see cref="DifficultySettings.AllUnlocked"/> zelf op zodra die ontgrendeling bestaat.
    /// </summary>
    public static class SandboxMode
    {
        public static void Apply(DifficultySettings settings, Ledger ledger, TileUnlocks tiles)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (tiles is null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            ledger.UnlimitedMoney = settings.UnlimitedMoney;
            if (settings.AllUnlocked)
            {
                tiles.UnlockAll();
            }
        }
    }
}
