using System;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Het budgetpaneel (E18-06) in UI Toolkit: belastingtarieven en dienstniveaus met min/plus-knoppen, de lening en de weekprognose.
    /// Alle logica zit in <see cref="BudgetPanelModel"/>; het paneel tekent de waarden opnieuw bij elke wijziging.
    /// </summary>
    public sealed class BudgetPanel : CityPanel, IDisposable
    {
        private readonly BudgetPanelModel _model;
        private readonly VisualElement _content;

        public BudgetPanel(BudgetPanelModel model)
            : base(FinanceText.BudgetTitle)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            AddToClassList(UiClasses.SettingsPanel);
            _content = new ScrollView(ScrollViewMode.Vertical) { name = "rows" };
            _content.AddToClassList(UiClasses.SettingsScroll);
            Add(_content);
            _model.Changed += Refresh;
            Refresh();
        }

        public BudgetPanelModel Model => _model;

        public void Dispose() => _model.Changed -= Refresh;

        /// <summary>Bouwt de regels opnieuw op uit het model.</summary>
        public void Refresh()
        {
            _content.Clear();
            var forecast = _model.Forecast();

            _content.Add(Section(FinanceText.ForecastSection));
            _content.Add(Line(FinanceText.Income, HudText.Money(forecast.Income)));
            _content.Add(Line(FinanceText.Expenses, HudText.Money(forecast.Expenses)));
            _content.Add(Line(FinanceText.Net, HudText.Money(forecast.Net)));
            _content.Add(Line(string.Empty, FinanceText.WeeksOfMoney(forecast.WeeksOfMoney(_model.Balance))));

            _content.Add(Section(FinanceText.TaxSection));
            foreach (var row in _model.TaxRows())
            {
                var captured = row;
                _content.Add(Stepper(
                    $"tax-{captured.Zone}-{captured.Density}",
                    $"{FinanceText.Zone(captured.Zone)}, {FinanceText.Density(captured.Density)}",
                    FinanceText.Percent(captured.Rate),
                    () => _model.StepTax(captured.Zone, captured.Density, -1),
                    () => _model.StepTax(captured.Zone, captured.Density, 1)));
            }

            _content.Add(Section(FinanceText.ServiceSection));
            foreach (var row in _model.ServiceRows())
            {
                var captured = row;
                _content.Add(Stepper(
                    $"service-{captured.Kind}",
                    $"{FinanceText.Service(captured.Kind)} ({HudText.Money(captured.WeeklyCost)}/week)",
                    FinanceText.Percent(captured.Funding),
                    () => _model.StepFunding(captured.Kind, -1),
                    () => _model.StepFunding(captured.Kind, 1)));
            }

            _content.Add(Section(FinanceText.LoanSection));
            _content.Add(Line(FinanceText.Debt, HudText.Money(_model.Debt)));
            _content.Add(Stepper("loan", HudText.Money(BudgetPanelModel.LoanStep), string.Empty, () => _model.Repay(), () => _model.Borrow(), FinanceText.Repay, FinanceText.Borrow));
        }

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList(UiClasses.SettingsSection);
            return label;
        }

        private static VisualElement Line(string name, string value)
        {
            var row = new VisualElement();
            row.AddToClassList(UiClasses.Keybind);
            var label = new Label(name);
            label.AddToClassList(UiClasses.KeybindLabel);
            row.Add(label);
            var amount = new Label(value);
            amount.AddToClassList(UiClasses.KeybindColumn);
            row.Add(amount);
            return row;
        }

        private static VisualElement Stepper(string name, string label, string value, Func<bool> down, Func<bool> up, string downText = "−", string upText = "+")
        {
            var row = new VisualElement { name = name };
            row.AddToClassList(UiClasses.Keybind);
            var title = new Label(label);
            title.AddToClassList(UiClasses.KeybindLabel);
            row.Add(title);
            row.Add(StepButton("down", downText, down));
            var amount = new Label(value) { name = "value" };
            amount.AddToClassList(UiClasses.KeybindColumn);
            row.Add(amount);
            row.Add(StepButton("up", upText, up));
            return row;
        }

        private static CityButton StepButton(string name, string text, Func<bool> action)
        {
            var button = new CityButton(text, ButtonVariant.Secondary) { name = name, Small = true };
            button.clicked += () => action();
            return button;
        }
    }
}
