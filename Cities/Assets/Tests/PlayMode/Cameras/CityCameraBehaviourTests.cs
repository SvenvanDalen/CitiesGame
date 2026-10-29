using System.Collections;
using System.IO;
using Cities.Gameplay.Cameras;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Cities.Tests.PlayMode.Cameras
{
    public class CityCameraBehaviourTests
    {
        private GameObject _camera;
        private InputActionAsset _controls;

        [SetUp]
        public void SetUp()
        {
            _controls = InputActionAsset.FromJson(File.ReadAllText("Assets/Input/CityControls.inputactions"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_camera);
            Object.Destroy(_controls);
        }

        [UnityTest]
        public IEnumerator CameraStartsAtConfiguredRigAndActionsAreBound()
        {
            _camera = new GameObject("City camera");
            _camera.SetActive(false);
            _camera.AddComponent<UnityEngine.Camera>();
            var behaviour = _camera.AddComponent<CityCameraBehaviour>();
            SetControls(behaviour, _controls);
            _camera.SetActive(true);

            yield return null;

            var rig = behaviour.Controller.Current;
            Assert.That(Vector3.Distance(_camera.transform.position, rig.Position), Is.LessThan(0.01f));
            Assert.That(rig.Distance, Is.EqualTo(600f).Within(0.01f));
            Assert.That(rig.Pitch, Is.EqualTo(50f).Within(0.01f));
            Assert.That(_controls.FindActionMap("Camera").enabled, Is.True);
            Assert.That(_controls.FindActionMap("Camera").actions.Count, Is.EqualTo(10));
        }

        private static void SetControls(CityCameraBehaviour behaviour, InputActionAsset controls)
        {
            // Veld is privé en geserialiseerd; in een scene wordt het in de inspector gezet.
            typeof(CityCameraBehaviour)
                .GetField("_controls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(behaviour, controls);
        }
    }
}
