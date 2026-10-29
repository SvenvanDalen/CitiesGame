using System;
using System.Collections.Generic;
using Cities.Gameplay.Controls;

namespace Cities.Tests.EditMode.Controls
{
    /// <summary>Bindingen zonder Input System: herbinden wacht tot de test <see cref="Press"/> aanroept.</summary>
    internal sealed class FakeControlBindings : IControlBindings
    {
        private readonly PathTable _paths = new PathTable();
        private SlotRef _pending;
        private Action<RebindOutcome> _completed;

        public FakeControlBindings() => Rows = ControlCatalog.Build(ControlTestData.Bindings());

        public event Action Changed;

        public IReadOnlyList<ControlRow> Rows { get; }

        public bool IsRebinding => _completed != null;

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

        public string GetDisplayName(SlotRef slot)
        {
            var path = _paths.Get(slot.Value.Binding);
            if (string.IsNullOrEmpty(path))
            {
                return "—";
            }

            var name = path.Substring(path.IndexOf('/') + 1);
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        public bool IsCustomized(SlotRef slot) => !BindingRules.SamePath(_paths.Get(slot.Value.Binding), slot.Value.Binding.DefaultPath);

        public void StartRebind(SlotRef slot, Action<RebindOutcome> completed)
        {
            _pending = slot;
            _completed = completed;
        }

        public void CancelRebind() => Finish(new RebindOutcome(RebindStatus.Canceled, _pending, default));

        /// <summary>Speler drukt op een toets terwijl er gewacht wordt.</summary>
        public void Press(string path)
        {
            var changes = RebindPlanner.Plan(Rows, _paths.Get, _pending, path);
            var swapped = default(SlotRef);
            foreach (var change in changes)
            {
                _paths.Set(change.Binding, change.Path);
                if (change.Swapped)
                {
                    swapped = RebindPlanner.Locate(Rows, change.Binding);
                }
            }

            var status = changes.Count == 0 ? RebindStatus.Unchanged : RebindStatus.Completed;
            Finish(new RebindOutcome(status, _pending, swapped));
            Changed?.Invoke();
        }

        public void ResetRow(ControlRow row)
        {
            foreach (var slot in row.Slots)
            {
                _paths.Set(slot.Binding, slot.Binding.DefaultPath);
            }

            Changed?.Invoke();
        }

        public void ResetAll()
        {
            foreach (var row in Rows)
            {
                ResetRow(row);
            }
        }

        private void Finish(RebindOutcome outcome)
        {
            var completed = _completed;
            _completed = null;
            completed?.Invoke(outcome);
        }
    }
}
