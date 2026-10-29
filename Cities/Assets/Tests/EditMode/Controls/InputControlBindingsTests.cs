using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cities.Core.Settings;
using Cities.Gameplay.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Cities.Tests.EditMode.Controls
{
    /// <summary>Werkt op een kopie van <c>CityControls.inputactions</c> met het echte Input System.</summary>
    [Category("UnityRuntime")]
    public class InputControlBindingsTests
    {
        private const string AssetPath = "Assets/Input/CityControls.inputactions";

        private InputActionAsset _asset;
        private MemorySettingsStore _store;
        private InputControlBindings _bindings;

        [SetUp]
        public void SetUp()
        {
            var original = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            _asset = Object.Instantiate(original);
            _store = new MemorySettingsStore();
            _bindings = new InputControlBindings(_asset, _store);
        }

        [TearDown]
        public void TearDown()
        {
            _bindings.Dispose();
            _asset.Disable();
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void EveryActionWithAKeyHasALabelAndTwoSlots()
        {
            Assert.That(ControlCatalog.FindUnlabeled(_bindings.Rows), Is.Empty);
            Assert.That(_bindings.Rows.Select(r => r.Key), Is.EquivalentTo(ControlLabels.All.Select(l => l.Key)));
            Assert.That(_bindings.Rows, Has.All.Matches<ControlRow>(r => r.Slots.Count == 2));
            Assert.That(_bindings.Rows.SelectMany(r => r.Slots).Count(s => !s.Rebindable), Is.EqualTo(1), "Alleen Escape voor het menu is vast");
        }

        [Test]
        public void DefaultKeysAreUnique()
        {
            var paths = _bindings.Rows.SelectMany(r => r.Slots).Select(s => s.Binding.DefaultPath).Where(p => p.Length > 0).ToList();

            Assert.That(paths.Select(p => p.ToLowerInvariant()), Is.Unique);
            Assert.That(paths, Has.All.Matches<string>(p => BindingRules.IsAssignable(p) || BindingRules.IsReserved(p)));
        }

        [Test]
        public void DisplayNamesComeFromTheInputSystem()
        {
            Assert.That(_bindings.GetDisplayName(Slot("Camera/Move/up", 0)), Is.EqualTo("W"));
            Assert.That(_bindings.GetDisplayName(Slot("Camera/Move/up", 1)), Is.EqualTo("Pijl omhoog"));
            Assert.That(_bindings.GetDisplayName(Slot("Camera/PanDrag", 0)), Is.EqualTo("Middelste muisknop"));
            Assert.That(_bindings.GetDisplayName(Slot("Camera/Rotate/negative", 1)), Is.EqualTo("—"));
            Assert.That(_bindings.HasCustomizations, Is.False);
        }

        [Test]
        public void AssignSwapsSavesAndChangesTheAction()
        {
            var changed = 0;
            _bindings.Changed += () => changed++;

            var outcome = _bindings.Assign(Slot("Camera/Move/up", 0), "<Keyboard>/space");

            Assert.That(outcome.Status, Is.EqualTo(RebindStatus.Completed));
            Assert.That(outcome.Swapped.Row.Key, Is.EqualTo("Game/Pause"));
            Assert.That(_asset.FindAction("Camera/Move").bindings[1].effectivePath, Is.EqualTo("<Keyboard>/space"));
            Assert.That(_asset.FindAction("Game/Pause").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/w"));
            Assert.That(_store.Contents.ContainsKey(InputControlBindings.SettingsName), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(_bindings.Assign(Slot("Camera/Move/up", 0), "<Keyboard>/space").Status, Is.EqualTo(RebindStatus.Unchanged));
            Assert.That(_bindings.Assign(Slot("Camera/Move/up", 0), "<Mouse>/leftButton").Status, Is.EqualTo(RebindStatus.Canceled));
        }

        [Test]
        public void SavedKeysSurviveARestartIncludingClearedSlots()
        {
            _bindings.Assign(Slot("Camera/Rotate/negative", 1), "<Keyboard>/q");
            _bindings.Assign(Slot("Camera/OrbitDrag", 1), "<Mouse>/forwardButton");

            var asset = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath));
            try
            {
                var restarted = new InputControlBindings(asset, _store);
                restarted.Load();

                Assert.That(restarted.GetDisplayName(Slot(restarted, "Camera/Rotate/negative", 1)), Is.EqualTo("Q"));
                Assert.That(restarted.GetDisplayName(Slot(restarted, "Camera/Rotate/negative", 0)), Is.EqualTo("—"));
                Assert.That(asset.FindAction("Camera/OrbitDrag").bindings[1].effectivePath, Is.EqualTo("<Mouse>/forwardButton"));
                restarted.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ResetRowRestoresDefaultsAndSwapsBack()
        {
            _bindings.Assign(Slot("Camera/Move/up", 0), "<Keyboard>/space");

            _bindings.ResetRow(Slot("Camera/Move/up", 0).Row);

            Assert.That(_bindings.HasCustomizations, Is.False, "Pauze kreeg spatie terug");
            Assert.That(_store.Contents.ContainsKey(InputControlBindings.SettingsName), Is.False, "Geen bestand zonder afwijkingen");
        }

        [Test]
        public void ResetAllRemovesEveryOverride()
        {
            _bindings.Assign(Slot("Camera/Move/up", 0), "<Keyboard>/i");
            _bindings.Assign(Slot("Game/SpeedFast", 1), "<Keyboard>/f2");

            _bindings.ResetAll();

            Assert.That(_bindings.HasCustomizations, Is.False);
            Assert.That(_store.Contents, Is.Empty);
        }

        [Test]
        public void UnreadableFileFallsBackToDefaults()
        {
            _store.Write(InputControlBindings.SettingsName, "{ dit is geen json");
            LogAssert.Expect(LogType.Warning, new Regex("Opgeslagen besturing is onleesbaar"));

            _bindings.Load();

            Assert.That(_bindings.HasCustomizations, Is.False);
        }

        [Test]
        public void TamperedFixedKeyIsIgnoredOnLoad()
        {
            var menu = _asset.FindAction("Game/Menu");
            menu.ApplyBindingOverride(0, "<Keyboard>/m");
            _store.Write(InputControlBindings.SettingsName, _asset.SaveBindingOverridesAsJson());

            _bindings.Load();

            Assert.That(menu.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
        }

        [Test]
        public void InteractiveRebindTakesTheNextKeyPress()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("RebindTestKeyboard");
            try
            {
                _asset.Enable();
                RebindOutcome outcome = default;
                var finished = false;
                _bindings.StartRebind(Slot("Game/Pause", 0), o =>
                {
                    outcome = o;
                    finished = true;
                });

                Assert.That(_bindings.IsRebinding, Is.True);
                Assert.That(_asset.FindActionMap("Game").enabled, Is.False, "Acties staan uit tijdens het wachten");
                Assert.That(() => _bindings.StartRebind(Slot("Game/Pause", 1), null), Throws.InvalidOperationException);

                Press(keyboard, Key.P);
                WaitForMatch(ref finished);

                Assert.That(outcome.Status, Is.EqualTo(RebindStatus.Completed));
                Assert.That(_asset.FindAction("Game/Pause").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/p"));
                Assert.That(_asset.FindActionMap("Game").enabled, Is.True);
                Assert.That(_bindings.IsRebinding, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void EscapeCancelsAnInteractiveRebind()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("RebindTestKeyboard");
            try
            {
                RebindOutcome outcome = default;
                var finished = false;
                _bindings.StartRebind(Slot("Game/Pause", 0), o =>
                {
                    outcome = o;
                    finished = true;
                });

                Press(keyboard, Key.Escape);
                WaitForMatch(ref finished);

                Assert.That(outcome.Status, Is.EqualTo(RebindStatus.Canceled));
                Assert.That(_bindings.HasCustomizations, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void FixedSlotCannotBeRebound()
        {
            Assert.That(() => _bindings.StartRebind(Slot("Game/Menu", 0), null), Throws.ArgumentException);
        }

        private static void Press(Keyboard keyboard, Key key)
        {
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                keyboard[key].WriteValueIntoEvent(1f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        /// <summary>Het Input System wacht na een match 0,1 s op een betere; daarna rondt een update het af.</summary>
        private static void WaitForMatch(ref bool finished)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!finished && deadline.ElapsedMilliseconds < 2000)
            {
                System.Threading.Thread.Sleep(20);
                InputSystem.Update();
            }

            Assert.That(finished, Is.True, "Herbinden rondde niet af");
        }

        private SlotRef Slot(string key, int slot) => Slot(_bindings, key, slot);

        private static SlotRef Slot(InputControlBindings bindings, string key, int slot) =>
            new SlotRef(ControlTestData.Row(bindings.Rows, key), slot);
    }

    internal sealed class MemorySettingsStore : ISettingsStore
    {
        public Dictionary<string, string> Contents { get; } = new Dictionary<string, string>();

        public bool TryRead(string name, out string contents) => Contents.TryGetValue(name, out contents);

        public void Write(string name, string contents) => Contents[name] = contents;

        public void Delete(string name) => Contents.Remove(name);
    }
}
