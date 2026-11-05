using Cities.Gameplay.Transit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Lijnbeheer voor het openbaar vervoer (E26-08): een knop linksonder opent een paneel met de haltes, een knop om van de losse haltes een
    /// lijn te maken, en per lijn het aantal bussen, de reizigers en het verwijderen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TransitPanelBehaviour : MonoBehaviour
    {
        [SerializeField] private TransitHostBehaviour _host;

        private CityPanel _panel;
        private VisualElement _bar;
        private int _lineCounter = 1;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _bar = new VisualElement { name = "transit-bar" };
            _bar.style.position = Position.Absolute;
            _bar.style.left = 8;
            _bar.style.bottom = 8;
            _bar.pickingMode = PickingMode.Ignore;
            var toggle = new CityButton("OV-lijnen");
            toggle.clicked += Toggle;
            _bar.Add(toggle);
            root.Add(_bar);

            _panel = new CityPanel("Openbaar vervoer");
            _panel.style.position = Position.Absolute;
            _panel.style.left = 8;
            _panel.style.bottom = 52;
            _panel.style.width = 320;
            _panel.style.display = DisplayStyle.None;
            _panel.CloseRequested += Toggle;
            root.Add(_panel);
        }

        private void OnDisable()
        {
            _bar?.RemoveFromHierarchy();
            _panel?.RemoveFromHierarchy();
        }

        private void Toggle()
        {
            var open = _panel.style.display == DisplayStyle.None;
            _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var transit = _host.Transit;
            _panel.Clear();
            _panel.Add(new Label("Haltes: " + System.Linq.Enumerable.Count(transit.Network.Stops) + " (zonder lijn: " + transit.UnlinkedStops().Count + ")"));
            var create = new CityButton("Lijn maken van losse haltes", ButtonVariant.Primary);
            create.clicked += () =>
            {
                transit.CreateLineFromUnlinked("Lijn " + _lineCounter++);
                Refresh();
            };
            _panel.Add(create);

            foreach (var line in transit.Network.Lines)
            {
                var id = line.Id;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 6;
                row.Add(new Label($"{line.Name}: {line.Vehicles} bussen, {transit.Fleet.Boardings(id)} instappers"));
                var less = new CityButton("−") { Small = true };
                less.clicked += () => { transit.AdjustVehicles(id, -1); Refresh(); };
                var more = new CityButton("+") { Small = true };
                more.clicked += () => { transit.AdjustVehicles(id, +1); Refresh(); };
                var remove = new CityButton("Verwijderen", ButtonVariant.Danger) { Small = true };
                remove.clicked += () => { transit.Network.RemoveLine(id); Refresh(); };
                row.Add(less);
                row.Add(more);
                row.Add(remove);
                _panel.Add(row);
            }
        }
    }
}
