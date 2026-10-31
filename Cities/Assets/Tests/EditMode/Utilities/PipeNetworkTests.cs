using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class PipeNetworkTests
    {
        [Test]
        public void EnoughSourceAndPipeGivesEveryoneFullWater()
        {
            var net = new PipeNetwork();
            var source = net.AddNode(100f, 0f);
            var a = net.AddNode(0f, 30f);
            var b = net.AddNode(0f, 20f);
            net.AddPipe(source, a, 100f);
            var ab = net.AddPipe(a, b, 50f);
            net.Solve();

            Assert.That(net.Satisfaction(a), Is.EqualTo(1f));
            Assert.That(net.Satisfaction(b), Is.EqualTo(1f));
            Assert.That(net.Load(ab), Is.EqualTo(20f / 50f).Within(1e-5f));
        }

        [Test]
        public void ThinPipeStarvesEverythingBehindItButNotWhatIsBefore()
        {
            var net = new PipeNetwork();
            var source = net.AddNode(1000f, 0f);
            var near = net.AddNode(0f, 10f);
            var far = net.AddNode(0f, 40f);
            net.AddPipe(source, near, 1000f);
            var thin = net.AddPipe(near, far, 20f);
            net.Solve();

            Assert.That(net.Satisfaction(near), Is.EqualTo(1f));
            Assert.That(net.Satisfaction(far), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(net.Load(thin), Is.EqualTo(2f), "overbelast");
        }

        [Test]
        public void SmallSourceSharesTheShortageProportionally()
        {
            var net = new PipeNetwork();
            var source = net.AddNode(25f, 0f);
            var a = net.AddNode(0f, 30f);
            var b = net.AddNode(0f, 20f);
            net.AddPipe(source, a, 100f);
            net.AddPipe(source, b, 100f);
            net.Solve();

            Assert.That(net.Satisfaction(a), Is.EqualTo(0.5f).Within(1e-5f), "25 aanbod voor 50 vraag");
            Assert.That(net.Satisfaction(b), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void ConsumersWithoutARouteGetNothingAndDemandFreeSourcesAreFine()
        {
            var net = new PipeNetwork();
            var source = net.AddNode(10f, 0f);
            var lonely = net.AddNode(0f, 5f);
            net.Solve();

            Assert.That(net.Satisfaction(lonely), Is.Zero);
            Assert.That(net.Satisfaction(source), Is.EqualTo(1f));
        }

        [Test]
        public void LoopsAndEitherPipeDirectionWork()
        {
            var net = new PipeNetwork();
            var source = net.AddNode(50f, 0f);
            var a = net.AddNode(0f, 10f);
            var b = net.AddNode(0f, 10f);
            net.AddPipe(a, source, 50f);
            net.AddPipe(b, a, 50f);
            net.AddPipe(source, b, 50f);
            net.Solve();

            Assert.That(net.Satisfaction(a), Is.EqualTo(1f));
            Assert.That(net.Satisfaction(b), Is.EqualTo(1f));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var net = new PipeNetwork();
            var a = net.AddNode(0f, 1f);
            var b = net.AddNode(1f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => net.AddNode(-1f, 0f));
            Assert.Throws<ArgumentException>(() => net.AddPipe(a, a, 1f));
            Assert.Throws<ArgumentException>(() => net.AddPipe(a, 9, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => net.AddPipe(a, b, 0f));
        }
    }
}
