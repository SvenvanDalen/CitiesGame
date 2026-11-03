using System;
using Cities.Simulation.Transit;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// De ondergrondse weergave (E09-06): een schakelaar waarmee het terrein doorzichtig wordt en tunnels en metrostations zichtbaar worden.
    /// <see cref="GroundOpacity"/> beweegt in <see cref="Step"/> soepel naar de gewenste waarde, zodat het wisselen niet springt; elke
    /// diepte krijgt een <see cref="DepthTint"/> zodat diepere lagen donkerder tekenen.
    /// </summary>
    public sealed class UndergroundView
    {
        /// <summary>Dekking van het terrein in de ondergrondse weergave: laag genoeg om erdoorheen te kijken, hoog genoeg om de ligging te houden.</summary>
        public const float UndergroundGroundOpacity = 0.15f;

        /// <summary>Hoe snel de dekking beweegt, in dekking per seconde.</summary>
        public const float FadeSpeed = 4f;

        public bool Enabled { get; private set; }

        /// <summary>Dekking van het terrein, 1 = ondoorzichtig.</summary>
        public float GroundOpacity { get; private set; } = 1f;

        /// <summary>Of de weergave klaar is met wisselen.</summary>
        public bool IsSettled => Enabled ? GroundOpacity <= UndergroundGroundOpacity : GroundOpacity >= 1f;

        public event Action<bool> Changed;

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
            {
                return;
            }

            Enabled = enabled;
            Changed?.Invoke(enabled);
        }

        public void Toggle() => SetEnabled(!Enabled);

        public void Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "De tijdstap kan niet negatief zijn.");
            }

            var target = Enabled ? UndergroundGroundOpacity : 1f;
            var step = FadeSpeed * deltaSeconds;
            GroundOpacity = math.clamp(target, GroundOpacity - step, GroundOpacity + step);
        }

        /// <summary>Of iets op <paramref name="depth"/> meter onder het maaiveld getekend wordt: ondergronds alleen in de ondergrondse weergave.</summary>
        public bool IsVisible(float depth) => depth <= 0f || Enabled;

        /// <summary>Helderheid van een ondergrondse laag, 1 aan het maaiveld tot 0,4 op de diepste ligging van een perron.</summary>
        public static float DepthTint(float depth) => math.lerp(1f, 0.4f, math.saturate(depth / Metro.MaxDepth));
    }
}
