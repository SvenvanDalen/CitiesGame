using System.Collections;
using Cities.Simulation.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cities.Tests.PlayMode.Timing
{
    public class SimulationClockBehaviourTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
        }

        [UnityTest]
        public IEnumerator ClockAdvancesWithRealTimeAndIgnoresTimeScale()
        {
            _host = new GameObject("Clock");
            var behaviour = _host.AddComponent<SimulationClockBehaviour>();
            var previousScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0f;
            try
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            finally
            {
                UnityEngine.Time.timeScale = previousScale;
            }

            Assert.That(behaviour.Clock.Tick, Is.InRange(3, 7));
        }

        [UnityTest]
        public IEnumerator PausedClockDoesNotTick()
        {
            _host = new GameObject("Clock");
            var behaviour = _host.AddComponent<SimulationClockBehaviour>();
            behaviour.Clock.SetSpeed(SimulationSpeed.Paused);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(behaviour.Clock.Tick, Is.EqualTo(0));
        }
    }
}
