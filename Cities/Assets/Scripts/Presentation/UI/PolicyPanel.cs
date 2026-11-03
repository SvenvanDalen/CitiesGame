using System;
using Cities.Simulation.Policies;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Het beleidspaneel (E18-06) in UI Toolkit: een lijst met maatregelen voor de stad of één district, met kosten, status en een knop om
    /// in te voeren of in te trekken. De logica zit in <see cref="PolicyPanelModel"/>; het paneel toont de uitkomst van elke klik onderaan.
    /// </summary>
    public sealed class PolicyPanel : CityPanel, IDisposable
    {
        private readonly PolicyPanelModel _model;
        private readonly VisualElement _content;
        private readonly Label _status;
        private int _district;

        public PolicyPanel(PolicyPanelModel model, int district = PolicyBook.CityWide)
            : base(FinanceText.PolicyTitle)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _district = district;
            AddToClassList(UiClasses.SettingsPanel);
            _content = new ScrollView(ScrollViewMode.Vertical) { name = "rows" };
            _content.AddToClassList(UiClasses.SettingsScroll);
            Add(_content);
            _status = new Label { name = "status" };
            _status.AddToClassList(UiClasses.TextCaption);
            _status.AddToClassList(UiClasses.SettingsStatus);
            Add(_status);
            _model.Changed += Refresh;
            Refresh();
        }

        public string StatusText => _status.text;

        /// <summary>Het getoonde gebied: <see cref="PolicyBook.CityWide"/> of een districtnummer.</summary>
        public int District
        {
            get => _district;
            set
            {
                _district = value;
                Refresh();
            }
        }

        public void Dispose() => _model.Changed -= Refresh;

        public void Refresh()
        {
            _content.Clear();
            _content.Add(Section(_district == PolicyBook.CityWide ? FinanceText.CityPolicies : $"{FinanceText.DistrictPolicies} {_district}"));
            foreach (var entry in _model.Entries(_district))
            {
                _content.Add(Row(entry));
            }
        }

        private VisualElement Row(PolicyEntry entry)
        {
            var row = new VisualElement { name = entry.Id };
            row.AddToClassList(UiClasses.Keybind);
            var title = new Label(entry.Name);
            title.AddToClassList(UiClasses.KeybindLabel);
            row.Add(title);

            var cost = new Label(Describe(entry));
            cost.AddToClassList(UiClasses.KeybindColumn);
            cost.AddToClassList(UiClasses.TextMuted);
            row.Add(cost);

            var button = new CityButton(entry.Active ? "Intrekken" : "Invoeren", entry.Active ? ButtonVariant.Danger : ButtonVariant.Primary) { name = "toggle", Small = true };
            button.SetEnabled(entry.CanToggle);
            button.clicked += () => _status.text = FinanceText.Result(_model.Toggle(entry.Id, _district), entry.Name);
            row.Add(button);
            return row;
        }

        private static string Describe(PolicyEntry entry)
        {
            if (entry.Locked && !entry.Active)
            {
                return FinanceText.Locked(entry.UnlockPopulation);
            }

            return $"{HudText.Money(entry.ActivationCost)}, {FinanceText.PerMonth(entry.MonthlyCost)}";
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList(UiClasses.SettingsSection);
            return label;
        }
    }
}
