using System.Collections;
using System.Linq;
using Cities.Gameplay.Services;
using Cities.Gameplay.Tools;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cities.Tests.PlayMode.MainScene
{
    /// <summary>Plaatsen via de echte toolketen en weer ongedaan maken (E01-12): object en dekking verdwijnen en komen terug met redo.</summary>
    public class PlacementUndoPlayTests
    {
        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator UndoingAServicePlacementRemovesItAndRedoBringsItBack()
        {
            var tools = Object.FindAnyObjectByType<ToolsBehaviour>();
            var services = Object.FindAnyObjectByType<ServicesHostBehaviour>();
            var police = Object.FindObjectsByType<PrefabPlacementBehaviour>().First(p => p.Tool.Name.Contains("Politie"));
            var controller = tools.Controller;
            controller.Select(police.Tool);

            var placed = false;
            for (var x = -300; x <= 300 && !placed; x += 60)
            {
                for (var z = -300; z <= 300 && !placed; z += 60)
                {
                    var pick = new PickResult(PickHit.None, new PickHit(PickKind.Terrain, 0, new float3(x, 0f, z), 100f));
                    controller.Update(new ToolInput(pick));
                    placed = controller.Preview.IsValid && controller.Apply();
                }
            }

            Assert.That(placed, Is.True, "ergens op de kaart past een politiebureau");
            yield return null;
            Assert.That(services.Services.Count, Is.EqualTo(1));
            var objects = Object.FindObjectsByType<PrefabPlacementBehaviour>().Sum(p => p.transform.childCount);

            Assert.That(controller.History.Undo(), Is.True);
            yield return null;
            Assert.That(services.Services.Count, Is.EqualTo(0), "de dienst is uit het model");
            Assert.That(Object.FindObjectsByType<PrefabPlacementBehaviour>().Sum(p => p.transform.childCount), Is.EqualTo(objects - 1), "het object is weg");

            Assert.That(controller.History.Redo(), Is.True);
            yield return null;
            Assert.That(services.Services.Count, Is.EqualTo(1), "redo zet hem terug");
        }
    }
}
