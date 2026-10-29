using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Core.Settings;
using UnityEngine.InputSystem;

namespace Cities.Gameplay.Controls
{
    /// <summary>
    /// Herbinden met het Input System op een <see cref="InputActionAsset"/>. Wijzigingen zijn overschrijvingen op het
    /// asset (het asset zelf verandert niet) en worden na elke wijziging opgeslagen onder <see cref="SettingsName"/>.
    /// </summary>
    public sealed class InputControlBindings : IControlBindings, IDisposable
    {
        public const string SettingsName = "controls";

        private const string EmptyDisplay = "—";

        private static readonly SystemLog Log = new SystemLog("Controls");

        private readonly InputActionAsset _asset;
        private readonly ISettingsStore _store;
        private readonly List<InputActionMap> _pausedMaps = new List<InputActionMap>();
        private readonly Func<BindingInfo, string> _effectivePath;
        private InputActionRebindingExtensions.RebindingOperation _operation;

        public InputControlBindings(InputActionAsset asset, ISettingsStore store)
        {
            _asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _effectivePath = EffectivePath;
            Rows = ControlCatalog.Build(Describe(asset));
        }

        public event Action Changed;

        public IReadOnlyList<ControlRow> Rows { get; }

        public bool IsRebinding => _operation != null;

        public bool HasCustomizations
        {
            get
            {
                foreach (var row in Rows)
                {
                    for (var slot = 0; slot < row.Slots.Count; slot++)
                    {
                        if (IsCustomized(new SlotRef(row, slot)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        /// <summary>Alle bindingen van het asset in de vorm van de catalogus.</summary>
        public static IEnumerable<BindingInfo> Describe(InputActionAsset asset)
        {
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (var i = 0; i < bindings.Count; i++)
                    {
                        var binding = bindings[i];
                        var part = binding.isPartOfComposite ? binding.name : string.Empty;
                        yield return new BindingInfo(map.name, action.name, i, part, binding.path, KindOf(binding), action.type == InputActionType.Button);
                    }
                }
            }
        }

        /// <summary>Laadt opgeslagen toetsen; een onleesbaar bestand geeft een waarschuwing en de standaardtoetsen.</summary>
        public void Load()
        {
            _asset.RemoveAllBindingOverrides();
            if (_store.TryRead(SettingsName, out var json) && !string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    _asset.LoadBindingOverridesFromJson(json);
                }
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is InvalidOperationException)
                {
                    Log.Warning("Opgeslagen besturing is onleesbaar; standaardtoetsen worden gebruikt. {0}", exception.Message);
                    _asset.RemoveAllBindingOverrides();
                }
            }

            RemoveOverridesOnFixedSlots();
            Changed?.Invoke();
        }

        public string GetDisplayName(SlotRef slot)
        {
            var binding = slot.Value.Binding;
            var path = EffectivePath(binding);
            if (string.IsNullOrEmpty(path))
            {
                return EmptyDisplay;
            }

            return KeyNames.For(path, FindAction(binding).GetBindingDisplayString(binding.Index, InputBinding.DisplayStringOptions.DontIncludeInteractions));
        }

        public bool IsCustomized(SlotRef slot)
        {
            var binding = slot.Value.Binding;
            return !BindingRules.SamePath(EffectivePath(binding), binding.DefaultPath);
        }

        public void StartRebind(SlotRef slot, Action<RebindOutcome> completed)
        {
            if (IsRebinding)
            {
                throw new InvalidOperationException("Er wordt al op een toets gewacht.");
            }

            if (!slot.IsValid || !slot.Value.Rebindable)
            {
                throw new ArgumentException("Het slot is niet herbindbaar.", nameof(slot));
            }

            var binding = slot.Value.Binding;
            string chosenPath = null;
            PauseMaps();
            var operation = FindAction(binding).PerformInteractiveRebinding(binding.Index)
                .WithExpectedControlType("Button")
                .WithCancelingThrough(BindingRules.CancelPath)
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(0.1f)
                .OnApplyBinding((_, path) => chosenPath = path)
                .OnComplete(_ => Finish(slot, chosenPath, completed))
                .OnCancel(_ => Finish(slot, null, completed));
            foreach (var device in BindingRules.AllowedDevices)
            {
                operation.WithControlsHavingToMatchPath(device);
            }

            foreach (var excluded in BindingRules.ExcludedControls)
            {
                operation.WithControlsExcluding(excluded);
            }

            _operation = operation;
            operation.Start();
        }

        public void CancelRebind() => _operation?.Cancel();

        /// <summary>Wijst een toets direct toe, zoals na een interactieve keuze (ook voor tests en scripts).</summary>
        public RebindOutcome Assign(SlotRef slot, string path)
        {
            if (!BindingRules.IsAssignable(path))
            {
                return new RebindOutcome(RebindStatus.Canceled, slot, default);
            }

            var changes = RebindPlanner.Plan(Rows, _effectivePath, slot, path);
            if (changes.Count == 0)
            {
                return new RebindOutcome(RebindStatus.Unchanged, slot, default);
            }

            var swapped = default(SlotRef);
            foreach (var change in changes)
            {
                Apply(change);
                if (change.Swapped)
                {
                    swapped = RebindPlanner.Locate(Rows, change.Binding);
                }
            }

            Save();
            Changed?.Invoke();
            return new RebindOutcome(RebindStatus.Completed, slot, swapped);
        }

        public void ResetRow(ControlRow row)
        {
            for (var slot = 0; slot < row.Slots.Count; slot++)
            {
                if (!row.Slots[slot].Rebindable)
                {
                    continue;
                }

                foreach (var change in RebindPlanner.Plan(Rows, _effectivePath, new SlotRef(row, slot), row.Slots[slot].Binding.DefaultPath))
                {
                    Apply(change);
                }
            }

            Save();
            Changed?.Invoke();
        }

        public void ResetAll()
        {
            _asset.RemoveAllBindingOverrides();
            Save();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            CancelRebind();
            _operation?.Dispose();
            _operation = null;
        }

        private static BindingKind KindOf(InputBinding binding)
        {
            if (binding.isComposite)
            {
                return BindingKind.Composite;
            }

            return binding.isPartOfComposite ? BindingKind.CompositePart : BindingKind.Simple;
        }

        private void Finish(SlotRef slot, string path, Action<RebindOutcome> completed)
        {
            _operation?.Dispose();
            _operation = null;
            ResumeMaps();
            var outcome = path == null ? new RebindOutcome(RebindStatus.Canceled, slot, default) : Assign(slot, path);
            completed?.Invoke(outcome);
        }

        private void Apply(BindingChange change)
        {
            var action = FindAction(change.Binding);
            if (change.IsDefault)
            {
                action.RemoveBindingOverride(change.Binding.Index);
            }
            else
            {
                action.ApplyBindingOverride(change.Binding.Index, change.Path);
            }
        }

        private void Save()
        {
            try
            {
                var json = _asset.SaveBindingOverridesAsJson();
                if (HasCustomizations)
                {
                    _store.Write(SettingsName, json);
                }
                else
                {
                    _store.Delete(SettingsName);
                }
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                Log.Error(exception, "Besturing kon niet worden opgeslagen.");
            }
        }

        private void RemoveOverridesOnFixedSlots()
        {
            foreach (var row in Rows)
            {
                foreach (var slot in row.Slots)
                {
                    if (!slot.Rebindable)
                    {
                        FindAction(slot.Binding).RemoveBindingOverride(slot.Binding.Index);
                    }
                }
            }
        }

        private void PauseMaps()
        {
            _pausedMaps.Clear();
            foreach (var map in _asset.actionMaps)
            {
                if (map.enabled)
                {
                    _pausedMaps.Add(map);
                    map.Disable();
                }
            }
        }

        private void ResumeMaps()
        {
            foreach (var map in _pausedMaps)
            {
                map.Enable();
            }

            _pausedMaps.Clear();
        }

        private string EffectivePath(BindingInfo binding) => FindAction(binding).bindings[binding.Index].effectivePath;

        private InputAction FindAction(BindingInfo binding) =>
            _asset.FindActionMap(binding.Map, throwIfNotFound: true).FindAction(binding.Action, throwIfNotFound: true);
    }
}
