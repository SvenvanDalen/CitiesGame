using System;
using Cities.Gameplay.Overlays;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Overlays
{
    public class OverlayControllerTests
    {
        private static readonly OverlayLegend Legend = new OverlayLegend("Test", "", 0f, 1f, OverlayColorScale.Hot);

        private static DelegateCellOverlaySource Source(string id, Func<long> version = null) =>
            new DelegateCellOverlaySource(id, id, 1, 1, Legend, (x, z) => 0f, version);

        private static OverlayController Controller()
        {
            var controller = new OverlayController();
            controller.Register(Source("a"));
            controller.Register(Source("b"));
            return controller;
        }

        [Test]
        public void RegisterRejectsDuplicatesAndNulls()
        {
            var controller = new OverlayController();
            controller.Register(Source("a"));

            Assert.Throws<ArgumentException>(() => controller.Register(Source("a")));
            Assert.Throws<ArgumentNullException>(() => controller.Register(null));
            Assert.That(controller.Sources, Has.Count.EqualTo(1));
            Assert.That(controller.Find("a"), Is.Not.Null);
            Assert.That(controller.Find("zzz"), Is.Null);
        }

        [Test]
        public void ActivateSelectsOneAndAnUnknownKeyChangesNothing()
        {
            var controller = Controller();
            var changed = 0;
            controller.Changed += () => changed++;

            Assert.That(controller.Activate("a"), Is.True);
            Assert.That(controller.Active.Id, Is.EqualTo("a"));
            Assert.That(controller.Activate("a"), Is.True);
            Assert.That(changed, Is.EqualTo(1), "opnieuw activeren meldt niets");
            Assert.That(controller.Activate("b"), Is.True);
            Assert.That(controller.Active.Id, Is.EqualTo("b"));
            Assert.That(controller.Activate("nope"), Is.False);
            Assert.That(controller.Active.Id, Is.EqualTo("b"));
        }

        [Test]
        public void ToggleSwitchesOnAndOff()
        {
            var controller = Controller();

            controller.Toggle("a");
            Assert.That(controller.Active?.Id, Is.EqualTo("a"));
            controller.Toggle("a");
            Assert.That(controller.Active, Is.Null);
            controller.Deactivate();
            Assert.That(controller.Active, Is.Null);
        }

        [Test]
        public void CycleWalksTheListAndEndsOff()
        {
            var controller = Controller();

            controller.Cycle();
            Assert.That(controller.Active.Id, Is.EqualTo("a"));
            controller.Cycle();
            Assert.That(controller.Active.Id, Is.EqualTo("b"));
            controller.Cycle();
            Assert.That(controller.Active, Is.Null);
            new OverlayController().Cycle();
        }

        [Test]
        public void UnregisteringTheActiveSourceTurnsItOff()
        {
            var controller = Controller();
            controller.Activate("a");

            Assert.That(controller.Unregister("a"), Is.True);
            Assert.That(controller.Active, Is.Null);
            Assert.That(controller.Unregister("a"), Is.False);
            Assert.That(controller.Sources, Has.Count.EqualTo(1));
        }

        [Test]
        public void NeedsRefreshFollowsTheSourceVersion()
        {
            long version = 1;
            var controller = new OverlayController();
            controller.Register(Source("v", () => version));

            Assert.That(controller.NeedsRefresh, Is.False, "niets actief");
            controller.MarkShown();
            controller.Activate("v");
            Assert.That(controller.NeedsRefresh, Is.True);
            controller.MarkShown();
            Assert.That(controller.NeedsRefresh, Is.False);
            version = 2;
            Assert.That(controller.NeedsRefresh, Is.True);
            controller.MarkShown();
            controller.Deactivate();
            controller.Activate("v");
            Assert.That(controller.NeedsRefresh, Is.True, "opnieuw aanzetten bakt opnieuw");
        }
    }
}
