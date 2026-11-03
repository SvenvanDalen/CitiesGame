using System.Collections.Generic;
using Cities.Core.Persistence;
using Cities.Simulation.Utilities;
using Cities.Tests.EditMode.Persistence;
using Cities.World.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class MorePowerSourcesTests
    {
        private static PowerPlantSpec Plant(PowerSource source, float fuel = 0f, float pollution = 0f) => new PowerPlantSpec
        {
            Source = source,
            FootprintWidth = 2,
            FootprintDepth = 2,
            CapacityMw = 100f,
            FuelCostPerMwh = fuel,
            AirPollution = pollution,
            MwhPerTonne = source == PowerSource.WasteToEnergy ? 0.6f : 0f,
            StorageMwh = source == PowerSource.Battery ? 200f : 0f,
            RoundTripEfficiency = source == PowerSource.Battery ? 0.9f : 0f,
        };

        private static List<string> Errors(PowerPlantSpec spec)
        {
            var errors = new List<string>();
            spec.Validate(errors);
            return errors;
        }

        [Test]
        public void ConstantSourcesAlwaysDeliverFullCapacity()
        {
            foreach (var source in new[] { PowerSource.Gas, PowerSource.Oil, PowerSource.Nuclear, PowerSource.Geothermal })
            {
                Assert.That(Plant(source).Output(0f, 0f), Is.EqualTo(100f), source.ToString());
            }
        }

        [Test]
        public void HydroFollowsTheRiverFlow()
        {
            var hydro = Plant(PowerSource.Hydro);

            Assert.That(hydro.Output(0.5f, 0f, 1f), Is.EqualTo(100f));
            Assert.That(hydro.Output(0.5f, 0f, 2f), Is.EqualTo(100f), "vlak boven het ontwerpdebiet");
            Assert.That(hydro.Output(0.5f, 0f, 0.5f), Is.EqualTo(50f));
            Assert.That(hydro.Output(0.5f, 0f, 0.05f), Is.Zero, "droogte");
        }

        [Test]
        public void SolarTowerKeepsDeliveringAfterSunsetButNotAllNight()
        {
            var tower = Plant(PowerSource.SolarTower);
            var solar = Plant(PowerSource.Solar);

            Assert.That(tower.Output(0.1f, 0f), Is.Zero, "nacht");
            Assert.That(tower.Output(0.5f, 0f), Is.EqualTo(100f), "middag, afgevlakt");
            Assert.That(solar.Output(0.8f, 0f), Is.Zero);
            Assert.That(tower.Output(0.8f, 0f), Is.GreaterThan(0f), "avond uit de warmteopslag");
            Assert.That(tower.Output(0.95f, 0f), Is.Zero, "diep in de nacht is de opslag leeg");
            Assert.That(tower.Output(1.5f, 0f), Is.EqualTo(100f), "de dag erna");
        }

        [Test]
        public void WasteToEnergyBurnsWhatItIsGivenUpToItsCapacity()
        {
            var plant = Plant(PowerSource.WasteToEnergy);

            Assert.That(plant.Output(0f, 0f, wasteTonnesPerHour: 50f), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(plant.Output(0f, 0f, wasteTonnesPerHour: 500f), Is.EqualTo(100f));
            Assert.That(plant.Output(0f, 0f), Is.Zero);
            Assert.That(Plant(PowerSource.Battery).Output(0f, 0f), Is.Zero);
        }

        [Test]
        public void SourceKindsAreClassifiedAndValidated()
        {
            Assert.That(Plant(PowerSource.Gas, 40f, 40f).BurnsFuel);
            Assert.That(Plant(PowerSource.Hydro).IsClean);
            Assert.That(Plant(PowerSource.Battery).IsDispatchable);
            Assert.That(Plant(PowerSource.Nuclear).IsDispatchable, Is.False);

            Assert.That(Errors(Plant(PowerSource.Gas, 40f, 40f)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Oil, 60f, 70f)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Nuclear, 8f)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Hydro)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Geothermal)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.SolarTower)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.WasteToEnergy, 0f, 20f)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Battery)), Is.Empty);

            Assert.That(Errors(Plant(PowerSource.Gas)), Has.Count.EqualTo(1), "gas zonder brandstofkosten");
            Assert.That(Errors(Plant(PowerSource.Geothermal, 1f)), Has.Count.EqualTo(1));
            Assert.That(Errors(Plant(PowerSource.Nuclear)), Has.Count.EqualTo(1), "kern zonder splijtstofkosten");
            Assert.That(Errors(Plant(PowerSource.Nuclear, 8f, 5f)), Has.Count.EqualTo(1), "kern met luchtuitstoot");

            var noRatio = Plant(PowerSource.WasteToEnergy);
            noRatio.MwhPerTonne = 0f;
            Assert.That(Errors(noRatio), Has.Count.EqualTo(1));

            var noStorage = Plant(PowerSource.Battery);
            noStorage.StorageMwh = 0f;
            Assert.That(Errors(noStorage), Has.Count.EqualTo(1));
            noStorage.StorageMwh = 10f;
            noStorage.RoundTripEfficiency = 1.2f;
            Assert.That(Errors(noStorage), Has.Count.EqualTo(1));
        }

        [Test]
        public void BatteriesChargeWithLossesAndDischargeWhatIsStored()
        {
            var storage = new PowerStorage();
            var id = storage.Add(50f, 100f, 0.81f);

            Assert.That(storage.Balance(40f, 1f), Is.Zero, "alles opgenomen");
            Assert.That(storage.Stored(id), Is.EqualTo(36f).Within(1e-3f), "40 MWh geladen bij 90% eenrichtingsrendement");
            Assert.That(storage.Balance(-20f, 1f), Is.Zero);
            Assert.That(storage.Stored(id), Is.EqualTo(36f - 20f / 0.9f).Within(1e-3f));
            Assert.That(storage.TotalStored, Is.EqualTo(storage.Stored(id)));
        }

        [Test]
        public void BatteriesAreLimitedByPowerAndCapacity()
        {
            var storage = new PowerStorage();
            var id = storage.Add(10f, 5f, 1f);

            Assert.That(storage.Balance(30f, 1f), Is.EqualTo(25f), "maximaal 5 MWh opslag bij 1 uur: 5 MW opgenomen");
            Assert.That(storage.Stored(id), Is.EqualTo(5f));
            Assert.That(storage.Balance(30f, 0.1f), Is.EqualTo(30f), "vol");
            Assert.That(storage.Balance(-30f, 0.25f), Is.EqualTo(-20f), "begrensd door het vermogen van 10 MW");
            Assert.That(storage.Balance(-30f, 1f), Is.EqualTo(-27.5f), "nog 2,5 MWh over: 2,5 MW een uur lang");
            Assert.That(storage.Stored(id), Is.Zero);
            Assert.That(storage.Balance(0f, 1f), Is.Zero);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => storage.Balance(1f, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => storage.Add(0f, 1f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => storage.Add(1f, 1f, 1.1f));
        }

        [Test]
        public void BatteriesRoundTripThroughASave()
        {
            var storage = new PowerStorage();
            storage.Add(50f, 100f, 0.9f);
            storage.Add(10f, 20f, 0.8f);
            storage.Balance(30f, 1f);
            var bytes = SaveBytes.Write(new SaveMetadata(), storage);
            var loaded = new PowerStorage();
            loaded.Add(1f, 1f, 1f);

            SaveBytes.Read(bytes, loaded);

            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded.TotalStored, Is.EqualTo(storage.TotalStored));
            Assert.That(SaveBytes.Write(new SaveMetadata(), loaded), Is.EqualTo(bytes));
        }

        [Test]
        public void ACorruptBatterySaveIsRejectedAndChangesNothing()
        {
            var storage = new PowerStorage();
            storage.Add(1f, 1f, 1f);

            Assert.Throws<SaveFormatException>(() => storage.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.Throws<SaveFormatException>(() => storage.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(1);
                w.Write(10f);
                w.Write(10f);
                w.Write(1f);
                w.Write(50f); // meer opgeslagen dan er past
            }), 1));
            Assert.That(storage.Count, Is.EqualTo(1));
        }
    }
}
