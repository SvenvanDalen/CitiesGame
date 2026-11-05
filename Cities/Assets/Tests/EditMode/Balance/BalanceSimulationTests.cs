using System;
using System.Collections.Generic;
using System.Text;
using Cities.Gameplay.Economy;
using Cities.Gameplay.NewCity;
using Cities.Gameplay.Population;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Balance
{
    /// <summary>
    /// Een kale speelsessie zonder Unity (E26-15): een script bouwt zoals een beginnende speler, het economie- en bevolkingsmodel
    /// rekenen de dagen door. Dit toetst de balans van startkapitaal, vraag en groei; het is geen vervanging voor echt spelen.
    /// </summary>
    public class BalanceSimulationTests
    {
        private const int TicksPerDay = 100;
        private const int TicksPerSecond = 10;
        private const long RoadAndZoneCostPerBuilding = 4_000;

        /// <summary>Hoeveel spelweken er in <paramref name="minutes"/> minuten spelen op normale snelheid passen.</summary>
        public static int WeeksIn(int minutes) => minutes * 60 * TicksPerSecond / TicksPerDay / Ledger.DaysPerWeek;

        public static string Run(Difficulty difficulty, int minutes, out CityEconomy economy, out CityPopulation population)
        {
            economy = new CityEconomy(difficulty);
            population = new CityPopulation();
            var report = new StringBuilder();
            report.AppendLine("| Minuut | Week | Inwoners | Woningcapaciteit | Banen | Saldo | Vraag wonen | Vraag werk |");
            report.AppendLine("|---|---|---|---|---|---|---|---|");
            var days = minutes * 60 * TicksPerSecond / TicksPerDay;
            for (var day = 1; day <= days; day++)
            {
                population.Daily();
                BuildLikeABeginner(economy, population, day);
                if (day % Ledger.DaysPerWeek == 0)
                {
                    var week = Ledger.WeekOf(day);
                    economy.EndWeek(week, population);
                    if (week % 2 == 0)
                    {
                        var minute = day * TicksPerDay / TicksPerSecond / 60;
                        report.AppendLine($"| {minute} | {week} | {population.Population:N0} | {population.HomeCapacity:N0} | {population.Jobs:N0} | {economy.Balance:N0} | {population.Demand.Residential:0.00} | {population.Demand.Work:0.00} |");
                    }
                }
            }

            return report.ToString();
        }

        // De speler zet zones neer zolang de vraag het toelaat en het geld er is: eerst wonen, dan werk en winkels.
        private static void BuildLikeABeginner(CityEconomy economy, CityPopulation population, int day)
        {
            var week = Ledger.WeekOf(day);

            // Na de eerste weken zet de speler diensten en parken neer: per vier weken wat meer aantrekkingskracht, tot een grens.
            population.ServicesAppeal = Math.Min(0.8f, 0.5f + (0.05f * (week / 4)));
            population.LandValueAppeal = Math.Min(0.7f, 0.5f + (0.03f * (week / 4)));
            if (population.DemandFor(Cities.World.Grid.ZoneType.Residential) > 0.1f && economy.TryBuild(week, RoadAndZoneCostPerBuilding))
            {
                population.AddBuilding(4, 0, 0);
            }

            if (population.DemandFor(Cities.World.Grid.ZoneType.Industrial) > 0.1f && economy.TryBuild(week, RoadAndZoneCostPerBuilding))
            {
                population.AddBuilding(0, 30, 0);
            }

            if (population.DemandFor(Cities.World.Grid.ZoneType.Commercial) > 0.1f && economy.TryBuild(week, RoadAndZoneCostPerBuilding))
            {
                population.AddBuilding(0, 14, 9000);
            }
        }

        [Test]
        public void ANormalStartSurvivesTheFirstHalfHourAndGrows()
        {
            var report = Run(Difficulty.Normal, 30, out var economy, out var population);
            TestContext.WriteLine(report);
            var path = Environment.GetEnvironmentVariable("CITIES_BALANCE_REPORT");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, report);
            }

            Assert.That(population.Population, Is.GreaterThan(0), "de stad groeit");
            Assert.That(economy.Balance, Is.GreaterThan(0), "de stad is na een halfuur niet failliet");
            Assert.That(WeeksIn(30), Is.GreaterThan(20));
        }

        [Test]
        public void TheEasyStartHasMoreMoneyThanTheHardStart()
        {
            Run(Difficulty.Easy, 5, out var easy, out _);
            Run(Difficulty.Hard, 5, out var hard, out _);
            Assert.That(easy.Settings.StartMoney, Is.GreaterThan(hard.Settings.StartMoney));
        }

        [Test]
        public void TheCityStillGrowsInTheSecondHalfHour()
        {
            Run(Difficulty.Normal, 30, out _, out var half);
            Run(Difficulty.Normal, 60, out _, out var hour);
            Assert.That(hour.Population, Is.GreaterThan(half.Population));
        }
    }
}
