using System;
using Cities.Simulation.Commands;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Commands
{
    public class CommandValidationTests
    {
        [Test]
        public void ValidHasNoReason()
        {
            var validation = CommandValidation.Valid(25);

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Reason, Is.Empty);
            Assert.That(validation.Cost, Is.EqualTo(25));
            Assert.That(validation.ToString(), Is.EqualTo("Valid (cost 25)"));
        }

        [Test]
        public void InvalidKeepsReasonAndCost()
        {
            var validation = CommandValidation.Invalid("insufficient-funds", 500);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Reason, Is.EqualTo("insufficient-funds"));
            Assert.That(validation.Cost, Is.EqualTo(500));
            Assert.That(validation.ToString(), Is.EqualTo("Invalid: insufficient-funds (cost 500)"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void InvalidRequiresReason(string reason)
        {
            Assert.Throws<ArgumentException>(() => CommandValidation.Invalid(reason));
        }

        [Test]
        public void EqualityComparesAllFields()
        {
            var a = CommandValidation.Invalid("x", 1);

            Assert.That(a == CommandValidation.Invalid("x", 1), Is.True);
            Assert.That(a != CommandValidation.Invalid("y", 1), Is.True);
            Assert.That(a != CommandValidation.Invalid("x", 2), Is.True);
            Assert.That(a.Equals((object)CommandValidation.Invalid("x", 1)), Is.True);
            Assert.That(a.Equals("x"), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(CommandValidation.Invalid("x", 1).GetHashCode()));
        }

        [Test]
        public void RecordRequiresCommand()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CommandRecord(1, 1, null, CommandOutcome.Executed, CommandValidation.Valid()));
        }
    }
}
