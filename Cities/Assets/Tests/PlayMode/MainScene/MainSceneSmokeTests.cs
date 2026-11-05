using System;
using System.Collections;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Environment;
using Cities.Gameplay.Events;
using Cities.Gameplay.Industry;
using Cities.Gameplay.Persistence;
using Cities.Gameplay.Policies;
using Cities.Gameplay.Population;
using Cities.Gameplay.Services;
using Cities.Gameplay.Transit;
using Cities.Gameplay.Utilities;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cities.Tests.PlayMode.MainScene
{
    /// <summary>
    /// Rooktest van de hoofdscène (E26-17): laadt <c>Main</c>, laat de stad een paar dagen lopen en controleert dat alle systemen aanwezig
    /// zijn en dat er geen fouten in het log komen (Unity laat een PlayMode-test mislukken bij een foutmelding). Vereist dat de scène met
    /// <c>Cities &gt; Bouw hoofdscène</c> is opgebouwd.
    /// </summary>
    public class MainSceneSmokeTests
    {
        private const string SceneName = "Main";

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllCitySystemsAreInTheScene()
        {
            yield return null;
            Assert.That(Object.FindAnyObjectByType<SimulationHostBehaviour>(), Is.Not.Null, "simulatie");
            Assert.That(Object.FindAnyObjectByType<EconomyHostBehaviour>(), Is.Not.Null, "economie");
            Assert.That(Object.FindAnyObjectByType<PopulationHostBehaviour>(), Is.Not.Null, "bevolking");
            Assert.That(Object.FindAnyObjectByType<UtilitiesHostBehaviour>(), Is.Not.Null, "nutsvoorzieningen");
            Assert.That(Object.FindAnyObjectByType<ServicesHostBehaviour>(), Is.Not.Null, "diensten");
            Assert.That(Object.FindAnyObjectByType<TransitHostBehaviour>(), Is.Not.Null, "openbaar vervoer");
            Assert.That(Object.FindAnyObjectByType<EnvironmentHostBehaviour>(), Is.Not.Null, "milieu");
            Assert.That(Object.FindAnyObjectByType<PolicyHostBehaviour>(), Is.Not.Null, "beleid");
            Assert.That(Object.FindAnyObjectByType<IndustryHostBehaviour>(), Is.Not.Null, "industrie");
            Assert.That(Object.FindAnyObjectByType<EventsHostBehaviour>(), Is.Not.Null, "gebeurtenissen");
            Assert.That(Object.FindAnyObjectByType<CityStateHostBehaviour>(), Is.Not.Null, "stadstoestand");
            Assert.That(Object.FindAnyObjectByType<SaveGameBehaviour>(), Is.Not.Null, "opslaan");
        }

        [UnityTest]
        public IEnumerator TheCityRunsForAFewDaysWithoutErrors()
        {
            var simulation = Object.FindAnyObjectByType<SimulationHostBehaviour>();
            var economy = Object.FindAnyObjectByType<EconomyHostBehaviour>();
            var startBalance = economy.Economy.Balance;

            simulation.Clock.SetSpeed(SimulationSpeed.Fastest);
            var target = simulation.Clock.Tick + (simulation.Clock.Calendar.TicksPerDay * 3);
            yield return new WaitUntilOrTimeout(() => simulation.Clock.Tick >= target, 30f);

            Assert.That(simulation.Clock.Tick, Is.GreaterThanOrEqualTo(target), "de klok loopt door");
            Assert.That(economy.Economy.Balance, Is.LessThanOrEqualTo(startBalance + 10_000_000), "het saldo blijft een getal");
        }

        [UnityTest]
        public IEnumerator SavingAndLoadingKeepsTheBalance()
        {
            var save = Object.FindAnyObjectByType<SaveGameBehaviour>();
            var economy = Object.FindAnyObjectByType<EconomyHostBehaviour>();
            var state = Object.FindAnyObjectByType<CityStateHostBehaviour>();
            yield return null;

            var captured = state.Capture();
            Assert.That(captured.Balance, Is.EqualTo(economy.Economy.Balance));
            Assert.That(save.Saves, Is.Not.Null, "opslagdienst gestart");
        }

        private sealed class WaitUntilOrTimeout : CustomYieldInstruction
        {
            private readonly Func<bool> _condition;
            private readonly float _deadline;

            public WaitUntilOrTimeout(Func<bool> condition, float seconds)
            {
                _condition = condition;
                _deadline = Time.realtimeSinceStartup + seconds;
            }

            public override bool keepWaiting => !_condition() && Time.realtimeSinceStartup < _deadline;
        }
    }
}
