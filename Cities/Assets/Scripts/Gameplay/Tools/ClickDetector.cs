using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Onderscheidt een klik van slepen: kort en bijna zonder te bewegen (E27-05).</summary>
    public static class ClickDetector
    {
        public const float MaxPixels = 6f;
        public const float MaxSeconds = 0.3f;

        public static bool IsClick(Vector2 downPosition, float downTime, Vector2 upPosition, float upTime)
            => (upPosition - downPosition).sqrMagnitude <= MaxPixels * MaxPixels && upTime - downTime <= MaxSeconds;
    }
}
