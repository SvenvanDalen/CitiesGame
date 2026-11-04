using System;
using Cities.Simulation.Economy;
using Cities.Simulation.Milestones;
using Cities.World.Tiles;

namespace Cities.Gameplay.NewCity
{
    /// <summary>
    /// Zet de zandbak-schakelaars van <see cref="DifficultySettings"/> (E22-04) door op de systemen: onbeperkt geld op het <see cref="Ledger"/>
    /// en alle kaarttegels ontgrendeld. Met een <see cref="UnlockService"/> (E16-03) staan ook alle mijlpalen open: gebouwen, tools, wegen,
    /// voertuigen en beleid zijn dan direct beschikbaar, zonder dat de gehaalde mijlpalen veranderen.
    /// </summary>
    public static class SandboxMode
    {
        public static void Apply(DifficultySettings settings, Ledger ledger, TileUnlocks tiles) => Apply(settings, ledger, tiles, null);

        /// <summary>Als <see cref="Apply(DifficultySettings, Ledger, TileUnlocks)"/>, en zet bij <see cref="DifficultySettings.AllUnlocked"/> ook alles open in <paramref name="unlocks"/> (mag <c>null</c> zijn).</summary>
        /// <returns>De definities die daardoor nu beschikbaar kwamen.</returns>
        public static System.Collections.Generic.IReadOnlyList<string> Apply(DifficultySettings settings, Ledger ledger, TileUnlocks tiles, UnlockService unlocks)
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

            return unlocks != null && settings.AllUnlocked
                ? unlocks.SetAllUnlocked(true)
                : System.Array.Empty<string>();
        }
    }
}
