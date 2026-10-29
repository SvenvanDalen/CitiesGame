using System.Collections.Generic;
using Cities.Gameplay.Controls;
using Cities.Presentation.UI;
using UnityEngine.UIElements;

namespace Cities.Presentation.Settings
{
    /// <summary>
    /// Paneel met alle herbindbare acties, gegroepeerd per categorie, met een primaire en een alternatieve toets per
    /// actie. Klik op een toets om te herbinden; een toets die al in gebruik is ruilt met de andere actie.
    /// </summary>
    public sealed class ControlsSettingsPanel : CityPanel, System.IDisposable
    {
        private const int SlotColumns = 2;

        private readonly IControlBindings _bindings;
        private readonly Label _status;
        private readonly CityButton _resetAll;
        private readonly List<RowView> _rows = new List<RowView>();

        public ControlsSettingsPanel(IControlBindings bindings)
            : base(ControlsSettingsText.Title)
        {
            _bindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings));
            AddToClassList(UiClasses.SettingsPanel);

            var hint = new Label(ControlsSettingsText.Hint);
            hint.AddToClassList(UiClasses.TextMuted);
            hint.AddToClassList(UiClasses.SettingsHint);
            Add(hint);

            Add(CreateHeader());
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "rows" };
            scroll.AddToClassList(UiClasses.SettingsScroll);
            Add(scroll);
            BuildRows(scroll);

            _status = new Label { name = "status" };
            _status.AddToClassList(UiClasses.TextCaption);
            _status.AddToClassList(UiClasses.SettingsStatus);
            Add(_status);

            _resetAll = AddFooterButton(ControlsSettingsText.ResetAll, ButtonVariant.Secondary, OnResetAll);
            _resetAll.name = "reset-all";
            AddFooterButton(ControlsSettingsText.Done, ButtonVariant.Primary, Done).name = "done";

            _bindings.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => _bindings.CancelRebind());
            Refresh();
        }

        public IControlBindings Bindings => _bindings;

        public string StatusText => _status.text;

        /// <summary>Stopt een lopende herbinding en luistert niet meer naar wijzigingen.</summary>
        public void Dispose()
        {
            _bindings.Changed -= Refresh;
            _bindings.CancelRebind();
        }

        /// <summary>Toetsknop van een regel, voor tests en toetsenbordnavigatie.</summary>
        public CityButton GetKeyButton(string rowKey, int slot)
        {
            foreach (var row in _rows)
            {
                if (row.Row.Key == rowKey)
                {
                    return row.Keys[slot];
                }
            }

            return null;
        }

        /// <summary>Werkt alle toetsnamen en knoppen bij, bijvoorbeeld na laden of herstellen.</summary>
        public void Refresh()
        {
            var rebinding = _bindings.IsRebinding;
            foreach (var view in _rows)
            {
                var customized = false;
                for (var slot = 0; slot < view.Keys.Length; slot++)
                {
                    var key = view.Keys[slot];
                    if (key == null)
                    {
                        continue;
                    }

                    var reference = new SlotRef(view.Row, slot);
                    var isCustom = _bindings.IsCustomized(reference);
                    customized |= isCustom;
                    var display = _bindings.GetDisplayName(reference);
                    key.text = display;
                    key.EnableInClassList(UiClasses.KeycapCustom, isCustom);
                    key.EnableInClassList(UiClasses.KeycapListening, false);
                    key.SetEnabled(view.Row.Slots[slot].Rebindable && !rebinding);
                    key.tooltip = TooltipFor(view.Row.Slots[slot], display);
                }

                view.Reset.style.visibility = customized ? Visibility.Visible : Visibility.Hidden;
                view.Reset.SetEnabled(!rebinding);
            }

            _resetAll.SetEnabled(!rebinding && _bindings.HasCustomizations);
        }

        private static string TooltipFor(ControlSlot slot, string display)
        {
            if (!slot.Rebindable)
            {
                return ControlsSettingsText.FixedTooltip;
            }

            return display == "—" ? ControlsSettingsText.EmptyTooltip : string.Empty;
        }

        private static VisualElement CreateHeader()
        {
            var header = new VisualElement { name = "header-row" };
            header.AddToClassList(UiClasses.Keybind);
            header.AddToClassList(UiClasses.KeybindHeader);
            var action = new Label(ControlsSettingsText.ColumnAction);
            action.AddToClassList(UiClasses.KeybindLabel);
            header.Add(action);
            foreach (var title in new[] { ControlsSettingsText.ColumnPrimary, ControlsSettingsText.ColumnAlternative })
            {
                var column = new Label(title);
                column.AddToClassList(UiClasses.KeybindColumn);
                header.Add(column);
            }

            var spacer = new VisualElement();
            spacer.AddToClassList(UiClasses.KeybindReset);
            header.Add(spacer);
            return header;
        }

        private void BuildRows(VisualElement container)
        {
            string category = null;
            foreach (var row in _bindings.Rows)
            {
                if (row.Category != category)
                {
                    category = row.Category;
                    var heading = new Label(category);
                    heading.AddToClassList(UiClasses.TextEmphasis);
                    heading.AddToClassList(UiClasses.SettingsSection);
                    container.Add(heading);
                }

                container.Add(CreateRow(row));
            }
        }

        private VisualElement CreateRow(ControlRow row)
        {
            var element = new VisualElement { name = row.Key };
            element.AddToClassList(UiClasses.Keybind);
            var label = new Label(row.Label);
            label.AddToClassList(UiClasses.KeybindLabel);
            element.Add(label);

            var keys = new CityButton[SlotColumns];
            for (var slot = 0; slot < SlotColumns; slot++)
            {
                if (slot >= row.Slots.Count)
                {
                    var empty = new VisualElement();
                    empty.AddToClassList(UiClasses.KeybindColumn);
                    element.Add(empty);
                    continue;
                }

                var reference = new SlotRef(row, slot);
                var key = new CityButton(string.Empty) { name = $"slot-{slot}" };
                key.AddToClassList(UiClasses.Keycap);
                key.AddToClassList(UiClasses.KeybindColumn);
                key.clicked += () => BeginRebind(reference);
                keys[slot] = key;
                element.Add(key);
            }

            var reset = new CityButton(ControlsSettingsText.Reset, ButtonVariant.Ghost) { name = "reset", tooltip = ControlsSettingsText.ResetTooltip };
            reset.Small = true;
            reset.AddToClassList(UiClasses.KeybindReset);
            reset.clicked += () =>
            {
                _bindings.ResetRow(row);
                _status.text = ControlsSettingsText.RowReset(row.Label);
            };
            element.Add(reset);

            _rows.Add(new RowView(row, keys, reset));
            return element;
        }

        /// <summary>Zoals een klik op de toetsknop van <paramref name="slot"/>.</summary>
        internal void BeginRebind(SlotRef slot)
        {
            if (_bindings.IsRebinding)
            {
                return;
            }

            _status.text = ControlsSettingsText.Listening(slot.Row.Label);
            _bindings.StartRebind(slot, OnRebindFinished);
            if (!_bindings.IsRebinding)
            {
                return;
            }

            // Tijdens het wachten zijn de andere knoppen uit, zodat een muisklik niet per ongeluk een tweede herbinding start.
            Refresh();
            var key = GetKeyButton(slot.Row.Key, slot.Slot);
            key.text = ControlsSettingsText.Waiting;
            key.EnableInClassList(UiClasses.KeycapListening, true);
        }

        private void OnRebindFinished(RebindOutcome outcome)
        {
            Refresh();
            _status.text = ControlsSettingsText.Outcome(outcome, _bindings.GetDisplayName);
        }

        private void OnResetAll()
        {
            _bindings.ResetAll();
            _status.text = ControlsSettingsText.ResetAllDone;
        }

        /// <summary>Zoals een klik op Klaar: stopt het wachten op een toets en vraagt om sluiten.</summary>
        internal void Done()
        {
            _bindings.CancelRebind();
            RequestClose();
        }

        private readonly struct RowView
        {
            public RowView(ControlRow row, CityButton[] keys, CityButton reset)
            {
                Row = row;
                Keys = keys;
                Reset = reset;
            }

            public ControlRow Row { get; }

            public CityButton[] Keys { get; }

            public CityButton Reset { get; }
        }
    }
}
