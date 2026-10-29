using System.Collections.Generic;
using System.Linq;
using Cities.Core.Configuration;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Configuration
{
    public class DefinitionDatabaseTests
    {
        private class FakeDefinition : IDefinition
        {
            private readonly string[] _problems;

            public FakeDefinition(string id, params string[] problems)
            {
                Id = id;
                _problems = problems;
            }

            public string Id { get; }

            public void Validate(ICollection<string> errors)
            {
                foreach (var problem in _problems)
                {
                    errors.Add(problem);
                }
            }
        }

        private sealed class FakeVehicle : FakeDefinition
        {
            public FakeVehicle(string id, params string[] problems)
                : base(id, problems)
            {
            }
        }

        [TestCase("vehicle.city-bus", true)]
        [TestCase("building.residential-low-1", true)]
        [TestCase("policy.free-transit.weekend", true)]
        [TestCase("vehicle", false)]
        [TestCase("Vehicle.bus", false)]
        [TestCase("vehicle.city_bus", false)]
        [TestCase("vehicle..bus", false)]
        [TestCase("vehicle.bus-", false)]
        [TestCase("1vehicle.bus", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IdRules(string id, bool valid)
        {
            Assert.That(DefinitionId.IsValid(id), Is.EqualTo(valid));
        }

        [Test]
        public void IdLengthIsLimited()
        {
            Assert.That(DefinitionId.IsValid("vehicle." + new string('a', DefinitionId.MaxLength)), Is.False);
            Assert.That(DefinitionId.KindOf("vehicle.city-bus"), Is.EqualTo("vehicle"));
            Assert.That(DefinitionId.KindOf("invalid"), Is.Empty);
        }

        [Test]
        public void LookupByTypeAndId()
        {
            var bus = new FakeVehicle("vehicle.city-bus");
            var database = new DefinitionDatabase(new IDefinition[] { bus, new FakeDefinition("policy.free-transit") });

            Assert.That(database.IsValid, Is.True);
            Assert.That(database.Count, Is.EqualTo(2));
            Assert.That(database.Get<FakeVehicle>("vehicle.city-bus"), Is.SameAs(bus));
            Assert.That(database.TryGet<FakeVehicle>("policy.free-transit", out _), Is.False, "Verkeerd type");
            Assert.That(database.TryGet<FakeVehicle>(null, out _), Is.False);
        }

        [Test]
        public void MissingOrWrongTypeGivesClearMessage()
        {
            var database = new DefinitionDatabase(new IDefinition[] { new FakeDefinition("policy.free-transit") });

            Assert.That(Assert.Throws<KeyNotFoundException>(() => database.Get<FakeVehicle>("vehicle.tram")).Message, Does.Contain("Geen definitie"));
            Assert.That(Assert.Throws<KeyNotFoundException>(() => database.Get<FakeVehicle>("policy.free-transit")).Message, Does.Contain("geen FakeVehicle"));
            Assert.That(Assert.Throws<KeyNotFoundException>(() => database.Get<FakeVehicle>(null)).Message, Does.Contain("Geen definitie"));
        }

        [Test]
        public void AllIsSortedByIdAndIndexIsStable()
        {
            var database = new DefinitionDatabase(new IDefinition[]
            {
                new FakeVehicle("vehicle.truck"),
                new FakeVehicle("vehicle.bus"),
                new FakeDefinition("policy.a"),
                new FakeVehicle("vehicle.car"),
            });

            Assert.That(database.All<FakeVehicle>().Select(v => v.Id), Is.EqualTo(new[] { "vehicle.bus", "vehicle.car", "vehicle.truck" }));
            Assert.That(database.All<FakeDefinition>().Count, Is.EqualTo(4), "Subtypes tellen mee");
            Assert.That(database.All<FakeVehicle>(), Is.SameAs(database.All<FakeVehicle>()), "Resultaat wordt hergebruikt");
            Assert.That(database.IndexOf<FakeVehicle>("vehicle.car"), Is.EqualTo(1));
            Assert.That(database.IndexOf<FakeVehicle>("vehicle.tram"), Is.EqualTo(-1));
        }

        [Test]
        public void ProblemsAreCollectedWithIds()
        {
            var database = new DefinitionDatabase(new IDefinition[]
            {
                new FakeVehicle("vehicle.bus", "te kort"),
                new FakeVehicle("vehicle.bus"),
                new FakeVehicle("Bus"),
                null,
                new FakeVehicle("vehicle.car"),
            });

            Assert.That(database.IsValid, Is.False);
            Assert.That(database.Errors, Has.Some.Contains("'vehicle.bus': te kort"));
            Assert.That(database.Errors, Has.Some.Contains("dubbel ID"));
            Assert.That(database.Errors, Has.Some.Contains("ongeldig ID"));
            Assert.That(database.Errors, Has.Some.Contains("Lege verwijzing"));
            Assert.That(database.TryGet<FakeVehicle>("vehicle.car", out _), Is.True, "Geldige definities blijven bruikbaar");
        }

        [Test]
        public void NullListIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => _ = new DefinitionDatabase(null));
        }
    }
}
