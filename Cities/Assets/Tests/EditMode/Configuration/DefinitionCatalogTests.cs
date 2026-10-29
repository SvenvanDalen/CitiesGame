using System.Collections.Generic;
using Cities.Core.Configuration;
using Cities.Simulation.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Configuration
{
    /// <summary>Maakt ScriptableObjects en gebruikt JsonUtility; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class DefinitionCatalogTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        [Test]
        public void ProjectCatalogContainsValidDefinitions()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<DefinitionCatalog>("Assets/Data/DefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null, "Catalogus ontbreekt; draai Cities > Definities > Catalogus bijwerken");

            var database = catalog.CreateDatabase();

            Assert.That(database.Errors, Is.Empty);
            Assert.That(database.All<VehicleDefinition>().Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(database.Get<VehicleDefinition>("vehicle.city-bus").Spec.PassengerCapacity, Is.EqualTo(80));
        }

        [Test]
        public void OverrideChangesCopyNotAsset()
        {
            var bus = CreateBus();
            var catalog = CreateCatalog(bus);
            var errors = new List<string>();

            var database = catalog.CreateDatabase(
                new Dictionary<string, string> { ["vehicle.city-bus"] = "{ \"_spec\": { \"MaxSpeedKmh\": 70 } }" }, errors);

            var overridden = database.Get<VehicleDefinition>("vehicle.city-bus");
            _created.Add(overridden);
            Assert.That(errors, Is.Empty);
            Assert.That(overridden.Spec.MaxSpeedKmh, Is.EqualTo(70f));
            Assert.That(overridden.Spec.PassengerCapacity, Is.EqualTo(80), "Niet genoemde velden blijven staan");
            Assert.That(bus.Spec.MaxSpeedKmh, Is.EqualTo(80f), "Asset blijft ongewijzigd");
        }

        [Test]
        public void OverrideCannotChangeIdAndUnknownIdsAreReported()
        {
            var catalog = CreateCatalog(CreateBus());
            var errors = new List<string>();

            var database = catalog.CreateDatabase(new Dictionary<string, string>
            {
                ["vehicle.city-bus"] = "{ \"_id\": \"vehicle.hacked\" }",
                ["vehicle.tram"] = "{}",
            }, errors);

            _created.Add(database.Get<VehicleDefinition>("vehicle.city-bus"));
            Assert.That(errors, Has.Some.Contains("ID kan niet"));
            Assert.That(errors, Has.Some.Contains("'vehicle.tram': er bestaat geen definitie"));
            Assert.That(database.TryGet<VehicleDefinition>("vehicle.hacked", out _), Is.False);
        }

        [Test]
        public void InvalidJsonKeepsOriginal()
        {
            var bus = CreateBus();
            var catalog = CreateCatalog(bus);
            var errors = new List<string>();

            var database = catalog.CreateDatabase(new Dictionary<string, string> { ["vehicle.city-bus"] = "{ niet json" }, errors);

            Assert.That(errors, Has.Some.Contains("ongeldige JSON"));
            Assert.That(database.Get<VehicleDefinition>("vehicle.city-bus"), Is.SameAs(bus));
        }

        [Test]
        public void DefinitionValidatesKindAndName()
        {
            var bus = CreateBus();
            var json = JsonUtility.ToJson(bus).Replace("vehicle.city-bus", "policy.bus").Replace("Stadsbus", " ");
            JsonUtility.FromJsonOverwrite(json, bus);
            var errors = new List<string>();

            bus.Validate(errors);

            Assert.That(errors, Has.Some.Contains("moet beginnen met 'vehicle.'"));
            Assert.That(errors, Has.Some.Contains("Weergavenaam"));
        }

        private VehicleDefinition CreateBus()
        {
            var bus = ScriptableObject.CreateInstance<VehicleDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{ \"_id\": \"vehicle.city-bus\", \"_displayName\": \"Stadsbus\", \"_spec\": { \"Category\": 3, \"LengthMeters\": 12, " +
                "\"WidthMeters\": 2.55, \"MaxSpeedKmh\": 80, \"AccelerationMs2\": 1.2, \"BrakingMs2\": 4, \"PassengerCapacity\": 80 } }",
                bus);
            _created.Add(bus);
            return bus;
        }

        private DefinitionCatalog CreateCatalog(params GameDefinition[] definitions)
        {
            var catalog = ScriptableObject.CreateInstance<DefinitionCatalog>();
            catalog.SetDefinitions(definitions);
            _created.Add(catalog);
            return catalog;
        }
    }
}
