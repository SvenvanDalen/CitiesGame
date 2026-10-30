using Cities.Presentation.Debugging;
using Cities.Presentation.UI;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class DebugPanelTextTests
    {
        [TestCase(0.4234, "0,42 ms")]
        [TestCase(12.345, "12,3 ms")]
        [TestCase(0, "0,00 ms")]
        public void MillisecondsUseADecimalComma(double value, string expected)
        {
            Assert.That(DebugPanelText.Milliseconds(value), Is.EqualTo(expected));
        }

        [TestCase(0L, "0")]
        [TestCase(1250000L, "1.250.000")]
        [TestCase(-3400L, "-3.400")]
        public void CountsUseDotsForThousands(long value, string expected)
        {
            Assert.That(DebugPanelText.Count(value), Is.EqualTo(expected));
        }

        [TestCase(1.0, 4.0, 0.25f)]
        [TestCase(9.0, 4.0, 1f)]
        [TestCase(-1.0, 4.0, 0f)]
        [TestCase(1.0, 0.0, 0f)]
        [TestCase(double.NaN, 4.0, 0f)]
        public void BarIsTheShareOfTheBudget(double ms, double budget, float expected)
        {
            Assert.That(DebugPanelText.BudgetFraction(ms, budget), Is.EqualTo(expected).Within(1e-6f));
        }

        [TestCase(2.0, 4.0, MeterColor.Success)]
        [TestCase(3.5, 4.0, MeterColor.Warning)]
        [TestCase(4.5, 4.0, MeterColor.Danger)]
        [TestCase(4.5, 0.0, MeterColor.Success)]
        public void ColorFollowsTheBudget(double ms, double budget, MeterColor expected)
        {
            Assert.That(DebugPanelText.BudgetColor(ms, budget), Is.EqualTo(expected));
        }

        [Test]
        public void TickHeaderNamesBudgetAndPopulation()
        {
            Assert.That(DebugPanelText.TickHeader(100000, 6.0), Is.EqualTo("Budget 6,00 ms per tick bij 100.000 inwoners"));
        }
    }
}
