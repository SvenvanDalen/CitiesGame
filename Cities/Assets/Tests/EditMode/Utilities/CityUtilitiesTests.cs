using Cities.Gameplay.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Utilities
{
    public class CityUtilitiesTests
    {
        [Test]
        public void BuildingWithoutPlantsGetsNothing()
        {
            var utilities = new CityUtilities();
            var house = utilities.AddBuilding(new float2(0, 0), households: 4, workplaces: 0);

            utilities.Tick();

            var status = utilities.StatusOf(house);
            Assert.That(status.Power, Is.EqualTo(0f));
            Assert.That(status.Water, Is.EqualTo(0f));
            Assert.That(status.IsConnected, Is.False);
        }

        [Test]
        public void NearbyPlantsSupplyTheBuilding()
        {
            var utilities = new CityUtilities();
            var house = utilities.AddBuilding(new float2(0, 0), households: 4, workplaces: 0);
            utilities.AddPlant(PlantKind.PowerPlant, new float2(20, 0));
            utilities.AddPlant(PlantKind.WaterPump, new float2(0, 20));
            utilities.AddPlant(PlantKind.SewagePlant, new float2(-20, 0));
            utilities.AddPlant(PlantKind.Landfill, new float2(0, -20));

            utilities.Tick();

            Assert.That(utilities.StatusOf(house).IsConnected, Is.True);
        }

        [Test]
        public void FarAwayPlantDoesNotConnect()
        {
            var utilities = new CityUtilities();
            var house = utilities.AddBuilding(new float2(0, 0), 4, 0);
            utilities.AddPlant(PlantKind.PowerPlant, new float2(5000, 0));

            utilities.Tick();

            Assert.That(utilities.StatusOf(house).Power, Is.EqualTo(0f));
        }

        [Test]
        public void OverloadedPlantGivesPartialSupply()
        {
            var utilities = new CityUtilities();
            utilities.AddPlant(PlantKind.PowerPlant, new float2(0, 0));
            var towers = new int[40];
            for (var i = 0; i < towers.Length; i++)
            {
                towers[i] = utilities.AddBuilding(new float2(i, 10), households: 0, workplaces: 400);
            }

            utilities.Tick();

            var power = utilities.StatusOf(towers[0]).Power;
            Assert.That(power, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void RemovedBuildingIsForgotten()
        {
            var utilities = new CityUtilities();
            var house = utilities.AddBuilding(new float2(0, 0), 1, 0);
            utilities.Remove(house);

            Assert.That(utilities.Count, Is.EqualTo(0));
            Assert.That(utilities.StatusOf(house).IsConnected, Is.True);
        }

        [Test]
        public void CapacityIsPositiveForEveryPlant()
        {
            foreach (PlantKind kind in System.Enum.GetValues(typeof(PlantKind)))
            {
                Assert.That(CityUtilities.CapacityOf(kind), Is.GreaterThan(0f));
            }
        }
    }
}
