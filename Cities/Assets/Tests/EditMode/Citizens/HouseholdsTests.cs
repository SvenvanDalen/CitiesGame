using System;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class HouseholdsTests
    {
        private static readonly EntityId<BuildingKind> HomeA = new EntityId<BuildingKind>(3, 1);
        private static readonly EntityId<BuildingKind> HomeB = new EntityId<BuildingKind>(4, 1);
        private static readonly EntityId<BuildingKind> Job = new EntityId<BuildingKind>(5, 1);

        private static Citizen Person(int age, bool home = true, bool work = false) =>
            new Citizen { Age = (byte)age, Household = 1, Home = home ? HomeA : default, Work = work ? Job : default };

        [Test]
        public void ASettledFamilyHasOneHomeAndOneHouseholdId()
        {
            var family = new[] { Person(40, false), Person(38, false), Person(5, false) };

            Households.Settle(family, 9, HomeB);

            Assert.That(family, Has.All.Matches<Citizen>(c => c.Home == HomeB && c.Household == 9u));
        }

        [Test]
        public void MovingTakesTheChildrenAlong()
        {
            var family = new[] { Person(40), Person(8) };

            Households.Settle(family, 1, HomeB);

            Assert.That(family[1].Home, Is.EqualTo(HomeB));
        }

        [Test]
        public void AFullHomeMakesTheHouseholdMove()
        {
            var family = new[] { Person(40, work: true), Person(38), Person(5) };

            Assert.That(Households.Evaluate(family, 2), Is.EqualTo(MoveReason.Overcrowded));
            Assert.That(Households.Evaluate(family, 3), Is.EqualTo(MoveReason.None));
        }

        [Test]
        public void NoWorkInTheHouseholdMakesItMoveButOneEarnerIsEnough()
        {
            Assert.That(Households.Evaluate(new[] { Person(40), Person(38, work: true) }, 4), Is.EqualTo(MoveReason.None));
            Assert.That(Households.Evaluate(new[] { Person(40), Person(5) }, 4), Is.EqualTo(MoveReason.NoWork));
            Assert.That(Households.Evaluate(new[] { Person(70), Person(5) }, 4), Is.EqualTo(MoveReason.None), "niemand die kan werken zoeken");
        }

        [Test]
        public void AHouseholdWithoutAHomeIsHomelessAndAnEmptyOneStays()
        {
            Assert.That(Households.Evaluate(new[] { Person(40, false) }, 4), Is.EqualTo(MoveReason.Homeless));
            Assert.That(Households.Evaluate(ReadOnlySpan<Citizen>.Empty, 4), Is.EqualTo(MoveReason.None));
        }

        [Test]
        public void OnlyAnAdultLeavesTheParents()
        {
            var child = Person(10);
            var adult = Person(20);

            Assert.That(Households.TryLeaveParents(ref child, 2, HomeB), Is.False);
            Assert.That(child.Home, Is.EqualTo(HomeA));
            Assert.That(Households.TryLeaveParents(ref adult, 0, HomeB), Is.False);
            Assert.That(Households.TryLeaveParents(ref adult, 2, default), Is.False);
            Assert.That(Households.TryLeaveParents(ref adult, 2, HomeB), Is.True);
            Assert.That(adult.Household, Is.EqualTo(2u));
            Assert.That(adult.Home, Is.EqualTo(HomeB));
        }

        [Test]
        public void AHouseholdIdOfZeroIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Households.Settle(new Citizen[1], 0, HomeA));
        }
    }
}
