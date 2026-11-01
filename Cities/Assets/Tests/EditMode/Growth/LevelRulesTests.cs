using Cities.Simulation.Growth;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Growth
{
    public class LevelRulesTests
    {
        private static LevelInputs All(float good, float pollution = 0f) => new LevelInputs(good, good, good, pollution);

        [Test]
        public void RequirementsRiseWithTheLevel()
        {
            Assert.That(LevelRules.Required(1), Is.Zero);
            Assert.That(LevelRules.Required(5), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(LevelRules.MaxPollution(1), Is.EqualTo(1f));
            Assert.That(LevelRules.MaxPollution(5), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(LevelRules.Required(9), Is.EqualTo(LevelRules.Required(5)), "buiten het bereik vastgezet");
        }

        [Test]
        public void ABuildingMovesUpOneLevelAtATime()
        {
            Assert.That(LevelRules.Next(1, All(1f)), Is.EqualTo(2), "ook bij alles op maximum maar één niveau per keer");
            Assert.That(LevelRules.Next(4, All(1f)), Is.EqualTo(5));
            Assert.That(LevelRules.Next(5, All(1f)), Is.EqualTo(5), "niet boven het maximum");
        }

        [Test]
        public void EveryConditionHoldsABuildingBack()
        {
            Assert.That(LevelRules.Next(1, new LevelInputs(0.1f, 1f, 1f, 0f)), Is.EqualTo(1), "landwaarde te laag");
            Assert.That(LevelRules.Next(1, new LevelInputs(1f, 0.1f, 1f, 0f)), Is.EqualTo(1), "te weinig diensten");
            Assert.That(LevelRules.Next(1, new LevelInputs(1f, 1f, 0.1f, 0f)), Is.EqualTo(1), "te laag opgeleid");
            Assert.That(LevelRules.Next(1, new LevelInputs(1f, 1f, 1f, 0.95f)), Is.EqualTo(1), "te veel vervuiling");
        }

        [Test]
        public void ABuildingOnTheEdgeDoesNotFlipBackAndForth()
        {
            var edge = All(0.4f);

            Assert.That(LevelRules.Next(3, edge), Is.EqualTo(3), "net genoeg voor niveau 3");
            var slightlyLess = All(0.35f);
            Assert.That(LevelRules.Next(3, slightlyLess), Is.EqualTo(3), "binnen de marge");
            Assert.That(LevelRules.Next(3, All(0.2f)), Is.EqualTo(2), "ver onder de eis: terug");
            Assert.That(LevelRules.Next(1, All(0f)), Is.EqualTo(1), "niveau 1 zakt niet verder");
        }

        [Test]
        public void PollutionAloneCanLowerALevel()
        {
            Assert.That(LevelRules.Next(5, All(1f, 0.6f)), Is.EqualTo(4));
        }
    }
}
