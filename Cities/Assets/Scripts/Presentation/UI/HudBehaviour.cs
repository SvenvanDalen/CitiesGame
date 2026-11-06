using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Simulation.Economy;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Services;
using Cities.Simulation.Timing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// De HUD bovenin (E26-06): geld, bevolking, datum en snelheid, met snelheidsknoppen, vraagmeters en een knop naar het begrotingspaneel.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;

        private Label _money;
        private Label _people;
        private Label _date;
        private CityMeter _residential;
        private CityMeter _commerce;
        private CityMeter _work;
        private BudgetPanel _budget;
        private VisualElement _bar;
        private readonly List<(CityButton Button, SimulationSpeed Speed)> _speedButtons = new List<(CityButton, SimulationSpeed)>();

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _bar = new VisualElement { name = "hud" };
            _bar.style.position = Position.Absolute;
            _bar.style.top = 8;
            _bar.style.left = 8;
            _bar.style.right = 8;
            _bar.style.flexDirection = FlexDirection.Row;
            _bar.style.alignItems = Align.Center;
            _bar.pickingMode = PickingMode.Ignore;

            _money = Badge();
            _people = Badge();
            _date = Badge();
            _bar.Add(_money);
            _bar.Add(_people);
            _bar.Add(_date);
            _bar.Add(SpeedButton("⏸", SimulationSpeed.Paused));
            _bar.Add(SpeedButton("1×", SimulationSpeed.Normal));
            _bar.Add(SpeedButton("2×", SimulationSpeed.Fast));
            _bar.Add(SpeedButton("3×", SimulationSpeed.Fastest));
            var budget = new CityButton("Geld en belasting");
            budget.clicked += ToggleBudget;
            _bar.Add(budget);
            _residential = Meter("Huizen");
            _commerce = Meter("Winkels");
            _work = Meter("Banen");
            root.Add(_bar);
        }

        private void OnDisable()
        {
            _budget?.Dispose();
            _bar?.RemoveFromHierarchy();
        }

        private void Update()
        {
            var clock = _simulation.Clock;
            var city = _population.Population;
            _money.text = HudText.Money(_economy.Economy.Balance);
            _people.text = "Inwoners " + HudText.Population(city.Population);
            _date.text = HudText.DateTime(clock.Calendar.Date, clock.Calendar.TimeOfDay);
            foreach (var (button, speed) in _speedButtons)
            {
                button.Selected = clock.Speed == speed;
            }

            Show(_residential, city.Demand.Residential);
            Show(_commerce, city.Demand.Commerce);
            Show(_work, city.Demand.Work);
        }

        private void ToggleBudget()
        {
            if (_budget != null)
            {
                _budget.Dispose();
                _budget.RemoveFromHierarchy();
                _budget = null;
                return;
            }

            var economy = _economy.Economy;
            var model = new BudgetPanelModel(
                economy.Taxes,
                economy.Budget,
                economy.Loans,
                economy.Ledger,
                () => (IEnumerable<ServiceStation>)economy.Stations,
                () => economy.WeeklyTaxIncome(_population.Population),
                () => Ledger.WeekOf(_simulation.Clock.Calendar.DaysElapsed));
            _budget = new BudgetPanel(model);
            _budget.style.position = Position.Absolute;
            _budget.style.top = 56;
            _budget.style.right = 8;
            _budget.CloseRequested += ToggleBudget;
            GetComponent<UIDocument>().rootVisualElement.Add(_budget);
        }

        private static void Show(CityMeter meter, float demand)
        {
            meter.Value = HudText.Meter(demand);
            meter.Color = HudText.MeterColorFor(demand);
        }

        private CityMeter Meter(string name)
        {
            var label = new Label(name);
            label.AddToClassList(UiClasses.Badge);
            label.style.marginLeft = 12;
            var meter = new CityMeter { name = name };
            meter.style.width = 60;
            meter.style.height = 8;
            _bar.Add(label);
            _bar.Add(meter);
            return meter;
        }

        private CityButton SpeedButton(string text, SimulationSpeed speed)
        {
            var button = new CityButton(text) { Small = true };
            button.clicked += () => _simulation.Clock.SetSpeed(speed);
            _speedButtons.Add((button, speed));
            return button;
        }

        private static Label Badge()
        {
            var label = new Label();
            label.AddToClassList(UiClasses.Badge);
            label.style.marginRight = 8;
            return label;
        }
    }
}
