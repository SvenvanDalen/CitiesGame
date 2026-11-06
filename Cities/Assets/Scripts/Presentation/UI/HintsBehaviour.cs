using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Gameplay.Services;
using Cities.Gameplay.Transit;
using Cities.Gameplay.Zoning;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>Toont onderin het scherm de eerstvolgende hint voor een beginnende speler (E26-15).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HintsBehaviour : MonoBehaviour
    {
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private ServicesHostBehaviour _services;
        [SerializeField] private TransitHostBehaviour _transit;
        [SerializeField] private Cities.Simulation.Network.RoadNetworkHostBehaviour _roads;

        private Label _label;
        private float _nextCheck;

        private void OnEnable()
        {
            if (_roads == null)
            {
                _roads = FindAnyObjectByType<Cities.Simulation.Network.RoadNetworkHostBehaviour>();
            }

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _label = new Label { name = "hint", pickingMode = PickingMode.Ignore };
            _label.style.position = Position.Absolute;
            _label.style.bottom = 150;
            _label.style.left = new Length(50f, LengthUnit.Percent);
            _label.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), 0f);
            _label.style.display = DisplayStyle.None;
            root.Add(_label);
        }

        private void OnDisable() => _label?.RemoveFromHierarchy();

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck || _population == null || _economy == null || _spawner == null)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + 1f;
            var population = _population.Population;
            var inputs = new HintInputs(
                _spawner.Built.Count,
                population.Jobs,
                population.Population,
                _services != null ? _services.Services.Count : 0,
                _transit != null ? System.Linq.Enumerable.Count(_transit.Transit.Network.Stops) : 0,
                _economy.Economy.Balance,
                _roads != null ? _roads.Network.SegmentCount : 1);
            var hint = CityHints.Next(inputs);
            _label.text = hint;
            _label.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
