using System;
using System.Collections.Generic;
using System.IO;
using Cities.Gameplay.Recording;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Recording
{
    public class InputRecordingTests
    {
        private static InputRecording Sample()
        {
            var recorder = new InputRecorder();
            recorder.Start(42, "0.7.1");
            recorder.Record(0, "Game/Pause", RecordedPhase.Performed);
            recorder.Record(10, "Camera/Pan", RecordedPhase.Started, 0.5f, -1.25f);
            recorder.Record(10, "Camera/Pan", RecordedPhase.Canceled);
            recorder.Record(25, "Tools/Apply", RecordedPhase.Performed, 1f);
            return recorder.Stop();
        }

        [Test]
        public void RecorderKeepsActionsInOrder()
        {
            var recording = Sample();

            Assert.That(recording.Seed, Is.EqualTo(42ul));
            Assert.That(recording.GameVersion, Is.EqualTo("0.7.1"));
            Assert.That(recording.Actions.Count, Is.EqualTo(4));
            Assert.That(recording.LastTick, Is.EqualTo(25));
        }

        [Test]
        public void TextRoundTripKeepsEverythingIncludingFloatsExactly()
        {
            var recording = Sample();
            var writer = new StringWriter();
            recording.Write(writer);

            var loaded = InputRecording.Read(new StringReader(writer.ToString()));

            Assert.That(loaded.Seed, Is.EqualTo(recording.Seed));
            Assert.That(loaded.GameVersion, Is.EqualTo(recording.GameVersion));
            Assert.That(loaded.Actions, Is.EqualTo(recording.Actions));
        }

        [Test]
        public void ActionsOutsideARecordingAreIgnored()
        {
            var recorder = new InputRecorder();

            Assert.That(recorder.Record(1, "A", RecordedPhase.Performed), Is.False);
            Assert.That(recorder.Count, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => recorder.Stop());
        }

        [Test]
        public void RecorderRejectsBadInput()
        {
            var recorder = new InputRecorder();
            recorder.Start(1, null);

            Assert.Throws<InvalidOperationException>(() => recorder.Start(2, "x"));
            Assert.Throws<ArgumentException>(() => recorder.Record(1, "met spatie", RecordedPhase.Performed));
            Assert.Throws<ArgumentException>(() => recorder.Record(1, "", RecordedPhase.Performed));
            recorder.Record(5, "A", RecordedPhase.Performed);
            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.Record(4, "A", RecordedPhase.Performed));
        }

        [Test]
        public void PlayerDeliversDueActionsPerTick()
        {
            var player = new InputPlayer();
            player.Begin(Sample());
            var seen = new List<RecordedAction>();

            Assert.That(player.Advance(0, seen.Add), Is.EqualTo(1));
            Assert.That(player.Advance(9, seen.Add), Is.EqualTo(0));
            Assert.That(player.Advance(10, seen.Add), Is.EqualTo(2));
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(player.Progress, Is.EqualTo(0.75f));
            Assert.That(player.Advance(1000, seen.Add), Is.EqualTo(1));

            Assert.That(player.IsPlaying, Is.False);
            Assert.That(seen, Is.EqualTo(Sample().Actions));
        }

        [Test]
        public void PlayerCanBeStoppedAndRestarted()
        {
            var player = new InputPlayer();
            Assert.That(player.Advance(5, _ => { }), Is.EqualTo(0));
            Assert.That(player.Progress, Is.EqualTo(1f));

            player.Begin(Sample());
            player.Advance(10, _ => { });
            player.Stop();
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.Total, Is.EqualTo(0));

            player.Begin(Sample());
            Assert.That(player.Played, Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => player.Begin(null));
            Assert.Throws<ArgumentNullException>(() => player.Advance(1, null));
        }

        [TestCase("")]
        [TestCase("geen opname")]
        [TestCase("cities-input 1\nversion 1\n")]
        [TestCase("cities-input 1\nseed 1\n")]
        [TestCase("cities-input 1\nseed 1\nversion 1\n1 A Performed 0")]
        [TestCase("cities-input 1\nseed 1\nversion 1\nx A Performed 0 0")]
        [TestCase("cities-input 1\nseed 1\nversion 1\n1 A Onbekend 0 0")]
        [TestCase("cities-input 1\nseed 1\nversion 1\n5 A Performed 0 0\n4 A Performed 0 0")]
        public void MalformedFilesAreRejected(string text)
        {
            Assert.Throws<FormatException>(() => InputRecording.Read(new StringReader(text)));
        }

        [Test]
        public void EmptyRecordingIsValid()
        {
            var writer = new StringWriter();
            new InputRecording(7, "v", Array.Empty<RecordedAction>()).Write(writer);

            var loaded = InputRecording.Read(new StringReader(writer.ToString() + "\n"));

            Assert.That(loaded.Actions, Is.Empty);
            Assert.That(loaded.LastTick, Is.EqualTo(0));
            Assert.That(new InputRecording(1, null, Array.Empty<RecordedAction>()).GameVersion, Is.EqualTo(string.Empty));
        }

        [Test]
        public void EqualityAndHashFollowAllFields()
        {
            var a = new RecordedAction(1, "A", RecordedPhase.Started, 1f, 2f);

            Assert.That(a.Equals((object)new RecordedAction(1, "A", RecordedPhase.Started, 1f, 2f)), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(new RecordedAction(1, "A", RecordedPhase.Started, 1f, 2f).GetHashCode()));
            Assert.That(a.Equals(new RecordedAction(1, "A", RecordedPhase.Started, 1f, 3f)), Is.False);
            Assert.That(a.Equals("x"), Is.False);
        }
    }
}
