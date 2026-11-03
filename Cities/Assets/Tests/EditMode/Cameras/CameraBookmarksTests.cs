using System;
using Cities.Core.Persistence;
using Cities.Gameplay.Cameras;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cameras
{
    public class CameraBookmarksTests
    {
        private static readonly CameraRig RigA = new CameraRig(new float3(100f, 5f, 200f), 40f, 35f, 300f);
        private static readonly CameraRig RigB = new CameraRig(new float3(900f, 0f, 50f), 200f, 60f, 80f);

        private static CameraController Controller() =>
            new CameraController(CameraSettings.Default, RigA, float2.zero, new float2(10_000f, 10_000f));

        [Test]
        public void StartsEmptyAndRejectsBadSlots()
        {
            var bookmarks = new CameraBookmarks();

            Assert.That(bookmarks.Has(0), Is.False);
            Assert.That(bookmarks.TryGet(4, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => bookmarks.Has(CameraBookmarks.Slots));
            Assert.Throws<ArgumentOutOfRangeException>(() => bookmarks.Set(-1, RigA));
        }

        [Test]
        public void UseSavesTheTargetAndRecallsIt()
        {
            var bookmarks = new CameraBookmarks();
            var controller = Controller();

            Assert.That(bookmarks.Use(2, true, controller), Is.EqualTo(BookmarkOutcome.Saved));
            controller.Teleport(RigB);
            Assert.That(bookmarks.Use(2, false, controller), Is.EqualTo(BookmarkOutcome.Recalled));

            Assert.That(controller.Target.Pivot.x, Is.EqualTo(100f).Within(0.01f));
            Assert.That(controller.Target.Distance, Is.EqualTo(300f).Within(0.01f));
        }

        [Test]
        public void RecallingAnEmptySlotChangesNothing()
        {
            var controller = Controller();

            Assert.That(new CameraBookmarks().Use(1, false, controller), Is.EqualTo(BookmarkOutcome.Empty));
            Assert.That(controller.Target.Distance, Is.EqualTo(RigA.Distance));
            Assert.That(controller.Target.Pivot.x, Is.EqualTo(RigA.Pivot.x));
        }

        [Test]
        public void SectionRoundTripsFilledAndEmptySlots()
        {
            var bookmarks = new CameraBookmarks();
            bookmarks.Set(0, RigA);
            bookmarks.Set(3, RigB);

            var bytes = SaveBytes.Write(new SaveMetadata(), new CameraBookmarksSection(bookmarks));
            var loaded = new CameraBookmarks();
            loaded.Set(1, RigA);
            SaveBytes.Read(bytes, new CameraBookmarksSection(loaded));

            Assert.That(loaded.TryGet(0, out var a), Is.True);
            Assert.That(a, Is.EqualTo(RigA));
            Assert.That(loaded.TryGet(3, out var b), Is.True);
            Assert.That(b, Is.EqualTo(RigB));
            Assert.That(loaded.Has(1), Is.False, "Laden vervangt de oude bladwijzers");
        }

        [Test]
        public void SectionDoesNotTouchTheBookmarksUntilApplied()
        {
            var bookmarks = new CameraBookmarks();
            var section = new CameraBookmarksSection(bookmarks);
            var reader = SaveBytes.ReaderFor(w =>
            {
                w.Write(CameraBookmarks.Slots);
                w.Write(true);
                foreach (var value in new[] { 1f, 2f, 3f, 4f, 5f, 6f })
                {
                    w.Write(value);
                }

                for (var i = 1; i < CameraBookmarks.Slots; i++)
                {
                    w.Write(false);
                }
            });

            var apply = section.Read(reader, 1);
            Assert.That(bookmarks.Has(0), Is.False);
            apply();

            Assert.That(bookmarks.Has(0), Is.True);
            Assert.That(section.Id, Is.EqualTo("CAMB"));
            Assert.That(section.Version, Is.EqualTo(1));
        }

        [Test]
        public void SectionRejectsBadContent()
        {
            var section = new CameraBookmarksSection(new CameraBookmarks());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(99)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Filled(float.NaN, 300f), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Filled(10f, 0f), 1));
            Assert.Throws<ArgumentNullException>(() => new CameraBookmarksSection(null));
        }

        private static SaveReader Filled(float pivotX, float distance) => SaveBytes.ReaderFor(w =>
        {
            w.Write(1);
            w.Write(true);
            foreach (var value in new[] { pivotX, 0f, 0f, 10f, 45f, distance })
            {
                w.Write(value);
            }
        });
    }
}
