using Cities.Gameplay.Overlays;
using UnityEngine;

namespace Cities.Presentation.Overlays
{
    /// <summary>
    /// Brengt de <see cref="UndergroundView"/> (E09-06) naar het scherm: stuurt elk frame de terreindekking als shaderglobal
    /// <c>_CityGroundOpacity</c>. Een knop of toets roept <see cref="Toggle"/> aan. shortcut: de terreinshader leest de global nog niet en
    /// de tunnelmeshes bestaan nog niet; beide volgen met de metrolijnen in de scene.
    /// </summary>
    public sealed class UndergroundViewBehaviour : MonoBehaviour
    {
        private static readonly int GroundOpacityId = Shader.PropertyToID("_CityGroundOpacity");

        public UndergroundView View { get; } = new UndergroundView();

        public void Toggle() => View.Toggle();

        private void Update()
        {
            View.Step(Time.unscaledDeltaTime);
            Shader.SetGlobalFloat(GroundOpacityId, View.GroundOpacity);
        }

        private void OnDisable() => Shader.SetGlobalFloat(GroundOpacityId, 1f);
    }
}
