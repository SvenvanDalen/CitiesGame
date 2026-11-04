using System;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Disasters
{
    public class ReliefForcesTests
    {
        private static ReliefForces Forces() => new ReliefForces { Teams = 2, Helicopters = 1, ShelterBeds = 5 };

        [Test]
        public void HelicoptersTakeTheCutOffFirstAndTeamsTheRoad()
        {
            var forces = Forces();
            var op = forces.Begin(30, 15, 8);

            var day = forces.Step();

            Assert.That(day.Rescued, Is.EqualTo(22), "10 per helikopter uit afgesneden gebied, 12 door twee teams");
            Assert.That(day.Died, Is.EqualTo(3), "10% van wie nog wacht, naar boven afgerond");
            Assert.That(day.Sheltered, Is.EqualTo(5), "vijf bedden");
            Assert.That(op.CutOff, Is.EqualTo(4));
            Assert.That(op.Trapped, Is.EqualTo(16));
            Assert.That(op.Homeless, Is.EqualTo(3));
        }

        [Test]
        public void LeftoverHelicopterCapacityHelpsOnTheRoad()
        {
            var forces = Forces();
            var op = forces.Begin(30, 15, 8);
            forces.Step();

            var day = forces.Step();

            Assert.That(day.Rescued, Is.EqualTo(20), "4 afgesneden, 16 over de weg met 6 extra van de helikopter");
            Assert.That(day.Died, Is.Zero);
            Assert.That(op.Rescued, Is.EqualTo(42));
            Assert.That(op.Died, Is.EqualTo(3));
            Assert.That(op.Days, Is.EqualTo(2));
            Assert.That(op.Rescued + op.Died, Is.EqualTo(45), "iedereen is gered of omgekomen");
            Assert.That(op.Finished, Is.False, "drie daklozen zonder bed");
        }

        [Test]
        public void WithoutHelpPeopleKeepDying()
        {
            var forces = new ReliefForces();
            var op = forces.Begin(10, 0, 0);

            var day = forces.Step();

            Assert.That(day.Rescued, Is.Zero);
            Assert.That(day.Died, Is.EqualTo(1));
            Assert.That(op.Trapped, Is.EqualTo(9));
        }

        [Test]
        public void ShelterBedsAreSharedOverTheDays()
        {
            var forces = new ReliefForces { ShelterBeds = 4 };
            var op = forces.Begin(0, 0, 3);
            forces.Step();
            op.Homeless += 3;

            var day = forces.Step();

            Assert.That(day.Sheltered, Is.EqualTo(1), "vier bedden, drie al bezet");
            Assert.That(op.Sheltered, Is.EqualTo(4));
            Assert.That(op.Homeless, Is.EqualTo(2));
        }

        [Test]
        public void AnOperationWithEveryoneHelpedIsFinished()
        {
            var forces = new ReliefForces { Teams = 1, ShelterBeds = 10 };
            var op = forces.Begin(5, 0, 5);

            forces.Step();

            Assert.That(op.Finished, Is.True);
            forces.End();
            Assert.That(forces.Operation, Is.Null);
            Assert.That(forces.Step(), Is.EqualTo(default(ReliefDay)), "zonder operatie gebeurt er niets");
        }

        [Test]
        public void BadInputIsRejected()
        {
            var forces = new ReliefForces();

            Assert.Throws<ArgumentOutOfRangeException>(() => forces.Teams = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => forces.Helicopters = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => forces.ShelterBeds = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => forces.Begin(-1, 0, 0));
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var forces = Forces();
            forces.Begin(30, 15, 8);
            forces.Step();
            var bytes = SaveBytes.Write(new SaveMetadata(), new ReliefSection(forces));

            var loaded = new ReliefForces();
            SaveBytes.Read(bytes, new ReliefSection(loaded));

            Assert.That(loaded.Teams, Is.EqualTo(2));
            Assert.That(loaded.Operation.Trapped, Is.EqualTo(16));
            Assert.That(loaded.Operation.Days, Is.EqualTo(1));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new ReliefSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new ReliefSection(loaded).Id, Is.EqualTo("RELF"));

            var idle = new ReliefForces { Teams = 1 };
            var idleBytes = SaveBytes.Write(new SaveMetadata(), new ReliefSection(idle));
            var loadedIdle = new ReliefForces();
            loadedIdle.Begin(1, 1, 1);
            SaveBytes.Read(idleBytes, new ReliefSection(loadedIdle));
            Assert.That(loadedIdle.Operation, Is.Null);
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ReliefSection(null));
            var section = new ReliefSection(new ReliefForces());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, -1, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 1, -5)), 1));
        }

        private static void Corrupt(SaveWriter w, int teams, int trapped)
        {
            w.Write(teams);
            w.Write(0);
            w.Write(0);
            w.Write(true);
            w.Write(trapped);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
        }
    }
}
