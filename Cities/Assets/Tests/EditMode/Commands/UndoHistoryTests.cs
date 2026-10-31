using System;
using System.Collections.Generic;
using Cities.Simulation.Commands;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Commands
{
    public class UndoHistoryTests
    {
        private long _money;

        // Een "bouwactie" die geld afschrijft en bij undo teruggeeft, zoals de echte handlers dat doen.
        private UndoAction Build(string name, long cost, List<string> log)
        {
            _money -= cost;
            log.Add(name);
            return new UndoAction(
                name,
                cost,
                () => { _money += cost; log.Remove(name); },
                () => { _money -= cost; log.Add(name); });
        }

        [Test]
        public void UndoGivesTheMoneyBackAndRedoChargesItAgain()
        {
            _money = 1000;
            var log = new List<string>();
            var history = new UndoHistory();
            history.Record(Build("weg", 300, log));

            Assert.That(_money, Is.EqualTo(700));
            Assert.That(history.Undo(), Is.True);
            Assert.That(_money, Is.EqualTo(1000));
            Assert.That(log, Is.Empty);
            Assert.That(history.Redo(), Is.True);
            Assert.That(_money, Is.EqualTo(700));
            Assert.That(log, Is.EqualTo(new[] { "weg" }));
        }

        [Test]
        public void ActionsComeBackInReverseOrderAndEmptyHistoryDoesNothing()
        {
            _money = 0;
            var log = new List<string>();
            var history = new UndoHistory();
            Assert.That(history.Undo(), Is.False);
            Assert.That(history.Redo(), Is.False);
            Assert.That(history.NextUndo, Is.Null);
            Assert.That(history.NextRedo, Is.Null);

            history.Record(Build("a", 1, log));
            history.Record(Build("b", 1, log));
            Assert.That(history.NextUndo.Description, Is.EqualTo("b"));
            history.Undo();

            Assert.That(log, Is.EqualTo(new[] { "a" }));
            Assert.That(history.NextRedo.Description, Is.EqualTo("b"));
            Assert.That(history.NextRedo.Cost, Is.EqualTo(1));
        }

        [Test]
        public void ANewActionAfterUndoDropsTheRedoChain()
        {
            _money = 0;
            var log = new List<string>();
            var history = new UndoHistory();
            history.Record(Build("a", 1, log));
            history.Undo();

            history.Record(Build("b", 1, log));

            Assert.That(history.RedoCount, Is.Zero);
            Assert.That(history.Redo(), Is.False);
        }

        [Test]
        public void OnlyTheLastTwentyActionsAreKept()
        {
            _money = 0;
            var log = new List<string>();
            var history = new UndoHistory();
            for (var i = 0; i < 25; i++)
            {
                history.Record(Build("x" + i, 1, log));
            }

            Assert.That(history.UndoCount, Is.EqualTo(20));
            while (history.Undo())
            {
            }

            Assert.That(log, Is.EqualTo(new[] { "x0", "x1", "x2", "x3", "x4" }), "de vijf oudste zijn niet meer terug te draaien");
            history.Clear();
            Assert.That(history.UndoCount, Is.Zero);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UndoHistory(0));
            Assert.Throws<ArgumentNullException>(() => new UndoHistory().Record(null));
            Assert.Throws<ArgumentNullException>(() => new UndoAction(null, 0, () => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => new UndoAction("a", 0, null, () => { }));
            Assert.Throws<ArgumentNullException>(() => new UndoAction("a", 0, () => { }, null));
        }
    }
}
