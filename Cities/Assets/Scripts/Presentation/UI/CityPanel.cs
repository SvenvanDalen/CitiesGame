using System;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Paneel met kop (titel en sluitknop), inhoud en een optionele voettekst voor knoppen.
    /// Kinderen in UXML komen in de inhoud terecht.
    /// </summary>
    [UxmlElement]
    public partial class CityPanel : VisualElement
    {
        private readonly Label _title;
        private readonly CityButton _close;
        private readonly VisualElement _body;

        public CityPanel()
            : this(string.Empty)
        {
        }

        public CityPanel(string title)
        {
            AddToClassList(UiClasses.Panel);

            var header = new VisualElement { name = "header" };
            header.AddToClassList(UiClasses.PanelHeader);
            _title = new Label(title) { name = "title" };
            _title.AddToClassList(UiClasses.PanelTitle);
            _close = new CityButton("×", ButtonVariant.Ghost) { name = "close", tooltip = "Sluiten" };
            _close.AddToClassList(UiClasses.PanelClose);
            _close.clicked += RequestClose;
            header.Add(_title);
            header.Add(_close);
            hierarchy.Add(header);

            _body = new VisualElement { name = "body" };
            _body.AddToClassList(UiClasses.PanelBody);
            hierarchy.Add(_body);

            Footer = new VisualElement { name = "footer" };
            Footer.AddToClassList(UiClasses.PanelFooter);
            Footer.style.display = DisplayStyle.None;
            hierarchy.Add(Footer);
        }

        /// <summary>Gebruiker klikt op de sluitknop; het paneel sluit zichzelf niet, de eigenaar beslist.</summary>
        public event Action CloseRequested;

        public override VisualElement contentContainer => _body;

        [UxmlAttribute]
        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }

        [UxmlAttribute]
        public bool Closable
        {
            get => _close.style.display != DisplayStyle.None;
            set => _close.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Balk onderaan voor knoppen; wordt zichtbaar zodra er een knop in staat.</summary>
        public VisualElement Footer { get; }

        /// <summary>Meldt dat het paneel dicht moet, zoals de sluitknop doet.</summary>
        protected void RequestClose() => CloseRequested?.Invoke();

        public CityButton AddFooterButton(string label, ButtonVariant variant, Action onClick)
        {
            var button = new CityButton(label, variant);
            if (onClick != null)
            {
                button.clicked += onClick;
            }

            Footer.Add(button);
            Footer.style.display = DisplayStyle.Flex;
            return button;
        }
    }
}
