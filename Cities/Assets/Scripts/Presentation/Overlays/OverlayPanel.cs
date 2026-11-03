using System;
using System.Collections.Generic;
using Cities.Gameplay.Overlays;
using Cities.Presentation.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.Overlays
{
    /// <summary>Paneel met een knop per overlay en de legenda (titel, kleurbalk en bereik) van de actieve overlay.</summary>
    public sealed class OverlayPanel : CityPanel, IDisposable
    {
        private const int GradientWidth = 128;

        private readonly OverlayController _controller;
        private readonly VisualElement _buttons;
        private readonly VisualElement _legend;
        private readonly Label _legendTitle;
        private readonly VisualElement _bar;
        private readonly Label _min;
        private readonly Label _max;
        private readonly List<(string Id, CityButton Button)> _rows = new List<(string, CityButton)>();
        private Texture2D _gradient;

        public OverlayPanel(OverlayController controller)
            : base(OverlayText.Title)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));

            _buttons = new VisualElement { name = "overlay-buttons" };
            Add(_buttons);

            _legend = new VisualElement { name = "overlay-legend" };
            _legendTitle = new Label { name = "legend-title" };
            _legendTitle.AddToClassList(UiClasses.TextEmphasis);
            _bar = new VisualElement { name = "legend-bar" };
            _bar.style.height = 12;
            _bar.style.marginTop = 4;
            _bar.style.marginBottom = 2;
            var labels = new VisualElement { name = "legend-labels" };
            labels.style.flexDirection = FlexDirection.Row;
            labels.style.justifyContent = Justify.SpaceBetween;
            _min = new Label();
            _max = new Label();
            _min.AddToClassList(UiClasses.TextCaption);
            _max.AddToClassList(UiClasses.TextCaption);
            labels.Add(_min);
            labels.Add(_max);
            _legend.Add(_legendTitle);
            _legend.Add(_bar);
            _legend.Add(labels);
            Add(_legend);

            _controller.Changed += Rebuild;
            Rebuild();
        }

        public void Dispose()
        {
            _controller.Changed -= Rebuild;
            if (_gradient != null)
            {
                UnityEngine.Object.Destroy(_gradient);
                _gradient = null;
            }
        }

        private void Rebuild()
        {
            _buttons.Clear();
            _rows.Clear();
            foreach (var source in _controller.Sources)
            {
                var id = source.Id;
                var button = new CityButton(source.Name, ButtonVariant.Ghost) { Small = true };
                button.Selected = ReferenceEquals(_controller.Active, source);
                button.clicked += () => _controller.Toggle(id);
                _buttons.Add(button);
                _rows.Add((id, button));
            }

            ShowLegend(_controller.Active?.Legend);
        }

        private void ShowLegend(OverlayLegend legend)
        {
            _legend.style.display = legend == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (legend == null)
            {
                return;
            }

            _legendTitle.text = legend.Title;
            _min.text = legend.Describe(legend.Min);
            _max.text = legend.Describe(legend.Max);
            if (_gradient == null)
            {
                _gradient = new Texture2D(GradientWidth, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            }

            var pixels = new Color32[GradientWidth];
            for (var i = 0; i < GradientWidth; i++)
            {
                var color = legend.Scale.Evaluate(i / (float)(GradientWidth - 1));
                pixels[i] = new Color32(color.R, color.G, color.B, 255);
            }

            _gradient.SetPixels32(pixels);
            _gradient.Apply(false);
            _bar.style.backgroundImage = new StyleBackground(_gradient);
        }
    }

    /// <summary>Teksten van het overlaypaneel.</summary>
    public static class OverlayText
    {
        public const string Title = "Overlays";
        public const string Toggle = "Overlays";
    }
}
