using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>Knop in de huisstijl, met een variant in plaats van losse USS-klassen.</summary>
    [UxmlElement]
    public partial class CityButton : Button
    {
        private ButtonVariant _variant;
        private bool _small;
        private bool _selected;

        public CityButton()
            : this(string.Empty, ButtonVariant.Secondary)
        {
        }

        public CityButton(string label, ButtonVariant variant = ButtonVariant.Secondary)
        {
            text = label;
            AddToClassList(UiClasses.Button);
            Variant = variant;
        }

        [UxmlAttribute]
        public ButtonVariant Variant
        {
            get => _variant;
            set
            {
                var previous = UiClasses.ForVariant(_variant);
                if (previous != null)
                {
                    RemoveFromClassList(previous);
                }

                _variant = value;
                var current = UiClasses.ForVariant(value);
                if (current != null)
                {
                    AddToClassList(current);
                }
            }
        }

        [UxmlAttribute]
        public bool Small
        {
            get => _small;
            set
            {
                _small = value;
                EnableInClassList(UiClasses.ButtonSmall, value);
            }
        }

        /// <summary>Gemarkeerd als actief, bijvoorbeeld de gekozen tool in de werkbalk.</summary>
        [UxmlAttribute]
        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                EnableInClassList(UiClasses.ButtonSelected, value);
            }
        }
    }
}
