using System.Collections.Generic;
using Cities.Gameplay.Policies;
using Cities.Simulation.Policies;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Beleid en districten (E26-10): een knop rechtsonder opent het beleidspaneel met stadsbeleid of beleid per district, en een
    /// strook om een district aan te maken, te kiezen en te schilderen met het districtpenseel.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PolicyPanelBehaviour : MonoBehaviour
    {
        [SerializeField] private PolicyHostBehaviour _host;

        private VisualElement _holder;
        private PolicyPanel _panel;
        private PolicyPanelModel _model;
        private VisualElement _districtBar;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _model = new PolicyPanelModel(
                _host.Policies.Book,
                _host.Policies.Specs,
                _host.Names,
                _host.Ledger,
                () => _host.Week,
                d => _host.Policies.PopulationIn(d, _host.CityPopulation));
            _panel = new PolicyPanel(_model);
            _holder = new VisualElement { name = "policy-holder" };
            _holder.style.position = Position.Absolute;
            _holder.style.right = 8;
            _holder.style.bottom = 52;
            _holder.style.width = 380;
            _holder.style.maxHeight = 520;
            _holder.style.display = DisplayStyle.None;
            _districtBar = new VisualElement();
            _districtBar.style.flexDirection = FlexDirection.Row;
            _districtBar.style.flexWrap = Wrap.Wrap;
            _holder.Add(_districtBar);
            _holder.Add(_panel);
            _panel.CloseRequested += Toggle;
            root.Add(_holder);

            var open = new CityButton("Beleid");
            open.style.position = Position.Absolute;
            open.style.right = 8;
            open.style.bottom = 8;
            open.clicked += Toggle;
            root.Add(open);
            RebuildBar();
        }

        private void OnDisable()
        {
            _panel?.Dispose();
            _holder?.RemoveFromHierarchy();
            _holder = null;
        }

        private void Toggle()
        {
            _holder.style.display = _holder.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RebuildBar()
        {
            _districtBar.Clear();
            Add("Stad", () => Choose(PolicyBook.CityWide));
            foreach (var info in _host.Policies.Districts.Book.Districts)
            {
                var id = info.Id;
                Add(info.Name, () => Choose(id));
            }

            Add("+ District", () =>
            {
                var id = _host.Policies.Districts.Book.Create("District " + (_host.Policies.Districts.Book.Count + 1));
                Choose(id);
                RebuildBar();
            });
        }

        private void Add(string text, System.Action action)
        {
            var button = new CityButton(text) { Small = true };
            button.clicked += action;
            _districtBar.Add(button);
        }

        private void Choose(int district)
        {
            _panel.District = district;
            if (district != PolicyBook.CityWide)
            {
                _host.Brush.ActiveDistrict = district;
                _host.Controller.Select(_host.Brush);
            }
        }
    }
}
