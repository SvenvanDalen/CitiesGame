using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>Horizontale balk voor een waarde tussen 0 en 1, bijvoorbeeld vraag, dekking of budget.</summary>
    [UxmlElement]
    public partial class CityMeter : VisualElement
    {
        private readonly VisualElement _fill;
        private float _value;
        private MeterColor _color;

        public CityMeter()
        {
            AddToClassList(UiClasses.Meter);
            _fill = new VisualElement { name = "fill", pickingMode = PickingMode.Ignore };
            _fill.AddToClassList(UiClasses.MeterFill);
            Add(_fill);
            Value = 0f;
        }

        /// <summary>Waarde tussen 0 en 1; daarbuiten wordt vastgezet.</summary>
        [UxmlAttribute]
        public float Value
        {
            get => _value;
            set
            {
                _value = Clamp01(value);
                _fill.style.width = Length.Percent(_value * 100f);
            }
        }

        [UxmlAttribute]
        public MeterColor Color
        {
            get => _color;
            set
            {
                var previous = UiClasses.ForMeterColor(_color);
                if (previous != null)
                {
                    RemoveFromClassList(previous);
                }

                _color = value;
                var current = UiClasses.ForMeterColor(value);
                if (current != null)
                {
                    AddToClassList(current);
                }
            }
        }

        internal static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
