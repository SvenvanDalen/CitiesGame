using System;
using Cities.Core.Persistence;
using Cities.Simulation.Fields;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class CarbonBalanceTests
    {
        [Test]
        public void EmissionsAddUpPerSourceAndForestsTakeSomeBack()
        {
            var balance = new CarbonBalance();

            balance.Emit(PollutionSource.Industry, 1000);
            balance.Emit(PollutionSource.Traffic, 500);
            balance.Emit(PollutionSource.Industry, 250);
            balance.Absorb(400);

            Assert.That(balance.Emitted(PollutionSource.Industry), Is.EqualTo(1250));
            Assert.That(balance.TotalEmitted, Is.EqualTo(1750));
            Assert.That(balance.Net, Is.EqualTo(1350));
            balance.Absorb(10_000);
            Assert.That(balance.Net, Is.EqualTo(0));
        }

        [Test]
        public void ClimateEffectsOnlyApplyWhenEnabledAndStayCapped()
        {
            var balance = new CarbonBalance();
            balance.Emit(PollutionSource.CoalPlant, CarbonBalance.TonnesPerDegree * 2);

            Assert.That(balance.Warming, Is.EqualTo(0f));
            Assert.That(balance.HeatwaveChanceFactor, Is.EqualTo(1f));

            balance.ClimateEffects = true;
            Assert.That(balance.Warming, Is.EqualTo(2f).Within(0.001f));
            Assert.That(balance.TemperatureShift, Is.EqualTo(2f).Within(0.001f));
            Assert.That(balance.HeatwaveChanceFactor, Is.EqualTo(1f + (2f * CarbonBalance.HeatwaveChancePerDegree)).Within(0.001f));

            balance.Emit(PollutionSource.CoalPlant, CarbonBalance.TonnesPerDegree * 50);
            Assert.That(balance.Warming, Is.EqualTo(CarbonBalance.MaxWarming));
        }

        [Test]
        public void RejectsNegativeAmounts()
        {
            var balance = new CarbonBalance();

            Assert.Throws<ArgumentOutOfRangeException>(() => balance.Emit(PollutionSource.Traffic, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => balance.Absorb(double.NaN));
        }

        [Test]
        public void SectionRoundTripsTheBalance()
        {
            var balance = new CarbonBalance { ClimateEffects = true };
            balance.Emit(PollutionSource.Industry, 123.5);
            balance.Emit(PollutionSource.CoalPlant, 9000);
            balance.Absorb(50);
            var bytes = SaveBytes.Write(new SaveMetadata(), new CarbonBalanceSection(balance));

            var loaded = new CarbonBalance();
            SaveBytes.Read(bytes, new CarbonBalanceSection(loaded));

            Assert.That(loaded.ClimateEffects, Is.True);
            Assert.That(loaded.Emitted(PollutionSource.Industry), Is.EqualTo(123.5));
            Assert.That(loaded.Emitted(PollutionSource.CoalPlant), Is.EqualTo(9000));
            Assert.That(loaded.Absorbed, Is.EqualTo(50));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new CarbonBalanceSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new CarbonBalanceSection(loaded).Id, Is.EqualTo("CO2B"));
            Assert.Throws<ArgumentNullException>(() => new CarbonBalanceSection(null));
        }
    }
}
