using System.Linq;
using Cities.Core.Configuration;
using Cities.Simulation.Vehicles;
using Cities.World.Buildings;
using Cities.World.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Configuration
{
    /// <summary>
    /// De placeholder-set (E21-04): elk gebouw, elke centrale en elk voertuig in de catalogus heeft een model, en elke voertuigcategorie heeft
    /// minstens één definitie. Leest assets en draait daarom alleen in Unity; ontbrekende voertuigmodellen bouw je met Cities > Modellen > Voertuigen bouwen.
    /// </summary>
    [Category("UnityRuntime")]
    public class PlaceholderSetTests
    {
        private static DefinitionDatabase Database() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/Data/DefinitionCatalog.asset").CreateDatabase();

        [Test]
        public void EveryBuildingAndPowerPlantHasAModelAndEveryVehicleASize()
        {
            var database = Database();

            Assert.That(database.All<BuildingDefinition>().Where(d => d.Prefab == null).Select(d => d.Id), Is.Empty);
            Assert.That(database.All<PowerPlantDefinition>().Where(d => d.Prefab == null).Select(d => d.Id), Is.Empty);

            // Voertuigen worden als geïnstantieerde agents getekend (CityAgent-shader) en hebben geen prefab; wel een afmeting voor de placeholder-kubus.
            Assert.That(database.All<VehicleDefinition>().Where(d => !(d.Spec.LengthMeters > 0f) || !(d.Spec.WidthMeters > 0f)).Select(d => d.Id), Is.Empty);
        }

        [Test]
        public void EveryVehicleCategoryHasADefinition()
        {
            var present = Database().All<VehicleDefinition>().Select(d => d.Spec.Category).ToHashSet();

            Assert.That(new[] { VehicleCategory.PassengerCar, VehicleCategory.Van, VehicleCategory.Truck, VehicleCategory.Bus, VehicleCategory.Tram, VehicleCategory.Service, VehicleCategory.Motorcycle, VehicleCategory.Bicycle }.Where(c => !present.Contains(c)), Is.Empty);
        }
    }
}
