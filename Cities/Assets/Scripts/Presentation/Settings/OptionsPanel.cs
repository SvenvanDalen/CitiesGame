using System;
using Cities.Core.Settings;
using Cities.Presentation.UI;
using UnityEngine.UIElements;

namespace Cities.Presentation.Settings
{
    /// <summary>
    /// Opties (E27-12): beeld, geluid, besturing en gameplay uit de <see cref="SettingsCatalog"/>, met een schuif, schakelaar of keuzeknop per
    /// instelling. Elke wijziging gaat direct naar <see cref="UserSettings"/>; <c>Toepassen</c> bewaart en past ze toe, <c>Standaard</c> herstelt.
    /// </summary>
    public sealed class OptionsPanel : CityPanel
    {
        private readonly UserSettings _settings;

        public OptionsPanel(UserSettings settings)
            : base("Opties")
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AddToClassList(UiClasses.SettingsPanel);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "option-rows" };
            scroll.AddToClassList(UiClasses.SettingsScroll);
            Add(scroll);
            Build(scroll);
            AddFooterButton("Standaard", ButtonVariant.Secondary, () =>
            {
                _settings.ResetAll();
                scroll.Clear();
                Build(scroll);
            }).name = "options-reset";
            AddFooterButton("Toepassen", ButtonVariant.Primary, () => Applied?.Invoke()).name = "options-apply";
        }

        /// <summary>De speler koos <c>Toepassen</c>: bewaren en doorvoeren.</summary>
        public event Action Applied;

        private void Build(VisualElement parent)
        {
            foreach (var category in _settings.Categories)
            {
                var title = new Label(category);
                title.AddToClassList(UiClasses.TextCaption);
                title.style.marginTop = 10;
                parent.Add(title);
                foreach (var definition in _settings.InCategory(category))
                {
                    parent.Add(Row(definition));
                }
            }
        }

        private VisualElement Row(SettingDefinition definition)
        {
            var row = new VisualElement { name = definition.Key };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            var label = new Label(definition.Label);
            label.style.flexGrow = 1;
            row.Add(label);
            row.Add(Control(definition));
            return row;
        }

        private VisualElement Control(SettingDefinition definition)
        {
            switch (definition.Kind)
            {
                case SettingKind.Slider:
                    var slider = new Slider(definition.Min, definition.Max) { value = _settings.Get(definition.Key) };
                    slider.style.width = 180;
                    slider.RegisterValueChangedCallback(e => _settings.Set(definition.Key, e.newValue));
                    return slider;
                case SettingKind.Choice:
                    var choice = new CityButton(definition.Choices[_settings.GetChoice(definition.Key)]);
                    choice.clicked += () =>
                    {
                        var next = (_settings.GetChoice(definition.Key) + 1) % definition.Choices.Count;
                        _settings.Set(definition.Key, next);
                        choice.text = definition.Choices[next];
                    };
                    return choice;
                default:
                    var toggle = new CityButton(OnOff(_settings.GetBool(definition.Key)));
                    toggle.clicked += () =>
                    {
                        _settings.SetBool(definition.Key, !_settings.GetBool(definition.Key));
                        toggle.text = OnOff(_settings.GetBool(definition.Key));
                    };
                    return toggle;
            }
        }

        private static string OnOff(bool on) => on ? "Aan" : "Uit";
    }
}
