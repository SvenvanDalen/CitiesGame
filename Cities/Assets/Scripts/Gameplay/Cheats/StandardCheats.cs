using System;
using System.Collections.Generic;
using System.Globalization;
using Cities.Simulation.Economy;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using Cities.World.Tiles;

namespace Cities.Gameplay.Cheats
{
    /// <summary>De systemen waar de standaardcheats op ingrijpen. Ontbrekende onderdelen laten hun commando's een melding geven.</summary>
    public sealed class CheatContext
    {
        public Ledger Ledger { get; set; }

        public TileUnlocks Unlocks { get; set; }

        public SimulationClock Clock { get; set; }

        public ISpawnService Spawner { get; set; }

        public IViewTeleporter Teleporter { get; set; }
    }

    /// <summary>De commando's geld, ontgrendel, tijd, spawn en teleport.</summary>
    public static class StandardCheats
    {
        /// <summary>Meer dan dit aantal dagen per commando laat de simulatie te lang stilstaan.</summary>
        public const int MaxSkipDays = 30;

        private const string TimeUsage = "Gebruik: tijd pauze, 1, 2, 3 of +<dagen>.";

        public static void Register(CheatConsole console, CheatContext context)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            console.Register("geld", "geld <bedrag>", "Voegt geld toe; een negatief bedrag trekt het af.", args => Money(context, args));
            console.Register("ontgrendel", "ontgrendel <x> <z> | alles", "Ontgrendelt een tegel (tegelcoördinaten) of alle tegels, gratis.", args => Unlock(context, args));
            console.Register("tijd", "tijd pauze|1|2|3|+<dagen>", $"Zet de snelheid of springt tot {MaxSkipDays} dagen vooruit.", args => Time(context, args));
            console.Register("spawn", "spawn <soort> <x> <z>", "Zet iets neer op een wereldpositie in meters.", args => Spawn(context, args));
            console.Register("teleport", "teleport <x> <z>", "Verplaatst de camera naar een wereldpositie in meters.", args => Teleport(context, args));
        }

        private static ConsoleResult Money(CheatContext context, IReadOnlyList<string> args)
        {
            if (context.Ledger == null || context.Clock == null)
            {
                return Missing("het geldmodel");
            }

            if (args.Count != 1 || !CheatConsole.TryParseLong(args[0], out var amount) || amount == 0)
            {
                return ConsoleResult.Error("Gebruik: geld <bedrag>, bijvoorbeeld geld 50000 of geld -2000.");
            }

            var week = Ledger.WeekOf(context.Clock.Calendar.DaysElapsed);
            if (amount > 0)
            {
                context.Ledger.Earn(week, LedgerCategory.Other, amount);
            }
            else
            {
                context.Ledger.Force(week, LedgerCategory.Other, -amount);
            }

            return ConsoleResult.Ok($"Saldo: {context.Ledger.Balance.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        private static ConsoleResult Unlock(CheatContext context, IReadOnlyList<string> args)
        {
            var unlocks = context.Unlocks;
            if (unlocks == null)
            {
                return Missing("de tegelontgrendeling");
            }

            if (args.Count == 1 && string.Equals(args[0], "alles", StringComparison.OrdinalIgnoreCase))
            {
                return ConsoleResult.Ok($"{UnlockAll(unlocks)} tegels ontgrendeld.");
            }

            if (args.Count != 2 || !CheatConsole.TryParseInt(args[0], out var tileX) || !CheatConsole.TryParseInt(args[1], out var tileZ))
            {
                return ConsoleResult.Error("Gebruik: ontgrendel <x> <z> of ontgrendel alles.");
            }

            var tile = new TileCoord(tileX, tileZ);
            if (!unlocks.Dimensions.Contains(tile))
            {
                return ConsoleResult.Error(TileUnlocks.OutsideMap);
            }

            return unlocks.ForceUnlock(tile)
                ? ConsoleResult.Ok($"Tegel {tileX},{tileZ} ontgrendeld.")
                : ConsoleResult.Error(TileUnlocks.AlreadyUnlocked);
        }

        private static int UnlockAll(TileUnlocks unlocks)
        {
            var opened = 0;
            for (var z = 0; z < unlocks.Dimensions.TilesZ; z++)
            {
                for (var x = 0; x < unlocks.Dimensions.TilesX; x++)
                {
                    opened += unlocks.ForceUnlock(new TileCoord(x, z)) ? 1 : 0;
                }
            }

            return opened;
        }

        private static ConsoleResult Time(CheatContext context, IReadOnlyList<string> args)
        {
            var clock = context.Clock;
            if (clock == null)
            {
                return Missing("de simulatieklok");
            }

            if (args.Count != 1)
            {
                return ConsoleResult.Error(TimeUsage);
            }

            var text = args[0];
            if (text.StartsWith("+", StringComparison.Ordinal))
            {
                return SkipDays(clock, text.Substring(1));
            }

            switch (text.ToLowerInvariant())
            {
                case "pauze":
                    clock.SetSpeed(SimulationSpeed.Paused);
                    break;
                case "1":
                    clock.SetSpeed(SimulationSpeed.Normal);
                    break;
                case "2":
                    clock.SetSpeed(SimulationSpeed.Fast);
                    break;
                case "3":
                    clock.SetSpeed(SimulationSpeed.Fastest);
                    break;
                default:
                    return ConsoleResult.Error(TimeUsage);
            }

            return ConsoleResult.Ok($"Snelheid: {clock.Speed}");
        }

        private static ConsoleResult SkipDays(SimulationClock clock, string text)
        {
            if (!CheatConsole.TryParseInt(text, out var days) || days < 1 || days > MaxSkipDays)
            {
                return ConsoleResult.Error($"Het aantal dagen ligt tussen 1 en {MaxSkipDays}.");
            }

            var ticks = (long)days * clock.Calendar.TicksPerDay;
            for (long i = 0; i < ticks; i++)
            {
                clock.Step();
            }

            return ConsoleResult.Ok($"{days} dagen vooruit: {clock.Calendar.Date}");
        }

        private static ConsoleResult Spawn(CheatContext context, IReadOnlyList<string> args)
        {
            var spawner = context.Spawner;
            if (spawner == null)
            {
                return Missing("het spawnen");
            }

            if (args.Count != 3 || !CheatConsole.TryParseFloat(args[1], out var x) || !CheatConsole.TryParseFloat(args[2], out var z))
            {
                return ConsoleResult.Error($"Gebruik: spawn <soort> <x> <z>. Soorten: {string.Join(", ", spawner.Kinds)}.");
            }

            var error = spawner.Spawn(args[0], x, z);
            return error == null ? ConsoleResult.Ok($"{args[0]} geplaatst op {args[1]},{args[2]}.") : ConsoleResult.Error(error);
        }

        private static ConsoleResult Teleport(CheatContext context, IReadOnlyList<string> args)
        {
            var teleporter = context.Teleporter;
            if (teleporter == null)
            {
                return Missing("de camera");
            }

            if (args.Count != 2 || !CheatConsole.TryParseFloat(args[0], out var x) || !CheatConsole.TryParseFloat(args[1], out var z))
            {
                return ConsoleResult.Error("Gebruik: teleport <x> <z>.");
            }

            var error = teleporter.TeleportTo(x, z);
            return error == null ? ConsoleResult.Ok($"Camera op {args[0]},{args[1]}.") : ConsoleResult.Error(error);
        }

        private static ConsoleResult Missing(string what) => ConsoleResult.Error($"Niet beschikbaar: {what} is in deze scene niet aangesloten.");
    }
}
