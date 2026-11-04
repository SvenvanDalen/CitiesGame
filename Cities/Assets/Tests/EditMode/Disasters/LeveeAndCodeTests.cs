using System;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class LeveeAndCodeTests
    {
        private static Levee Dike(float condition = 1f) => new Levee(1, new float2(0f, 0f), new float2(100f, 0f), 10f, 50f, condition);

        [Test]
        public void ALeveeHoldsUpToItsEffectiveCrest()
        {
            Assert.That(Dike().Holds(10f), Is.True);
            Assert.That(Dike().Holds(10.1f), Is.False);
            Assert.That(Dike(0.5f).EffectiveCrest, Is.EqualTo(9.5f));
            Assert.That(Dike(0f).EffectiveCrest, Is.EqualTo(9f));
            Assert.That(Dike(0f).Holds(9.5f), Is.False, "vervallen dijk bezwijkt eerder");
        }

        [Test]
        public void ALeveeCoversTheLandWithinItsReach()
        {
            var dike = Dike();

            Assert.That(dike.Covers(new float2(50f, 30f)), Is.True);
            Assert.That(dike.Covers(new float2(150f, 0f)), Is.True, "afstand tot het eindpunt");
            Assert.That(dike.Covers(new float2(50f, 60f)), Is.False);
            Assert.That(dike.Covers(new float2(200f, 0f)), Is.False);
            Assert.That(dike.Length, Is.EqualTo(100f));
        }

        [Test]
        public void ALeveeRejectsBadValuesAndTheConditionCanChange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Levee(1, float2.zero, new float2(1f, 0f), 5f, 0f));
            Assert.Throws<ArgumentException>(() => new Levee(1, float2.zero, float2.zero, 5f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Dike(1.5f));
            var dike = Dike();
            dike.SetCondition(0.2f);
            Assert.That(dike.Condition, Is.EqualTo(0.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => dike.SetCondition(-1f));
        }

        [Test]
        public void ALeveeKeepsTheWaterOutUntilItIsOvertopped()
        {
            var network = new LeveeNetwork();
            network.Add(float2.zero, new float2(100f, 0f), 10f, 50f);
            var behind = new float2(50f, 30f);

            Assert.That(network.Depth(behind, 8f, 2f), Is.Zero);
            Assert.That(network.Damage(behind, 8f, 2f, 10f), Is.Zero);
            Assert.That(network.Depth(behind, 11f, 2f), Is.EqualTo(9f), "overstort: net als zonder dijk");
            Assert.That(network.Damage(behind, 11f, 2f, 10f), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(network.Depth(new float2(50f, 500f), 8f, 2f), Is.EqualTo(6f), "buiten het bereik");
        }

        [Test]
        public void ANetworkPicksTheNearestHoldingLeveeAndRemovesById()
        {
            var network = new LeveeNetwork();
            var far = network.Add(new float2(0f, 40f), new float2(100f, 40f), 10f, 50f);
            var near = network.Add(new float2(0f, 10f), new float2(100f, 10f), 10f, 50f);
            var point = new float2(50f, 0f);

            Assert.That(network.ProtectorOf(point, 5f), Is.SameAs(near));
            Assert.That(network.Remove(near.Id), Is.True);
            Assert.That(network.ProtectorOf(point, 5f), Is.SameAs(far));
            Assert.That(network.Remove(99), Is.False);
            Assert.That(network.ProtectorOf(point, 50f), Is.Null);
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var network = new LeveeNetwork();
            network.Add(float2.zero, new float2(100f, 0f), 10f, 50f, 0.75f);
            network.Add(new float2(5f, 5f), new float2(5f, 90f), 4f, 20f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new LeveeSection(network));

            var loaded = new LeveeNetwork();
            SaveBytes.Read(bytes, new LeveeSection(loaded));

            Assert.That(loaded.Levees, Has.Count.EqualTo(2));
            Assert.That(loaded.NextId, Is.EqualTo(3));
            Assert.That(loaded.Levees[0].Condition, Is.EqualTo(0.75f));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new LeveeSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new LeveeSection(loaded).Id, Is.EqualTo("LEVE"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new LeveeSection(null));
            var section = new LeveeSection(new LeveeNetwork());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 0, 1, 10f, 0.5f, 100f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 9, 10f, 0.5f, 100f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 0f, 0.5f, 100f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 10f, 2f, 100f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 10f, 0.5f, 0f)), 1));
        }

        [Test]
        public void ReinforcedBuildingCostsMoreToBuildAndEvenMoreToRetrofit()
        {
            Assert.That(BuildingCodes.BuildCost(1000, BuildingCode.None), Is.EqualTo(900));
            Assert.That(BuildingCodes.BuildCost(1000, BuildingCode.Basic), Is.EqualTo(1000));
            Assert.That(BuildingCodes.BuildCost(1000, BuildingCode.Reinforced), Is.EqualTo(1250));
            Assert.That(BuildingCodes.RetrofitCost(1000, BuildingCode.Basic, BuildingCode.Reinforced), Is.EqualTo(375));
            Assert.That(BuildingCodes.RetrofitCost(1000, BuildingCode.None, BuildingCode.Basic), Is.EqualTo(150));
            Assert.That(BuildingCodes.RetrofitCost(1000, BuildingCode.Reinforced, BuildingCode.Basic), Is.Zero);
        }

        [Test]
        public void TheCityCodeIsTheMinimumNotAMaximum()
        {
            Assert.That(BuildingCodes.Apply(BuildingCode.Basic, BuildingCode.Reinforced), Is.EqualTo(BuildingCode.Reinforced));
            Assert.That(BuildingCodes.Apply(BuildingCode.Reinforced, BuildingCode.None), Is.EqualTo(BuildingCode.Reinforced));
            Assert.That(BuildingCodes.Apply(BuildingCode.None, BuildingCode.None), Is.EqualTo(BuildingCode.None));
        }

        [Test]
        public void ReinforcedCodeReducesEarthquakeDamage()
        {
            Assert.That(BuildingCodes.DamageAvoided(6f, BuildingCode.Basic, BuildingCode.Reinforced, 1), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BuildingCodes.DamageAvoided(3f, BuildingCode.Basic, BuildingCode.Reinforced, 1), Is.Zero, "te zwak om schade te doen");
        }

        [Test]
        public void CodesRejectBadInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingCodes.BuildCost(-1, BuildingCode.Basic));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingCodes.CostFactor((BuildingCode)99));
        }

        private static void Corrupt(SaveWriter w, int nextId, int id, float reach, float condition, float length)
        {
            w.Write(nextId);
            w.Write(1);
            w.Write(id);
            w.Write(0f);
            w.Write(0f);
            w.Write(length);
            w.Write(0f);
            w.Write(5f);
            w.Write(reach);
            w.Write(condition);
        }
    }
}
