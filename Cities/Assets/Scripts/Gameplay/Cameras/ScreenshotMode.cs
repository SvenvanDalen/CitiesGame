using System;
using System.Globalization;
using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Rekenregels van de screenshot-modus: ruimere camera-instellingen, opnamegrootte en bestandsnaam.</summary>
    public static class ScreenshotMode
    {
        /// <summary>Grootste zijde in pixels die de GPU als opname aankan.</summary>
        public const int MaxSide = 16384;

        /// <summary>Grootste vergroting van het scherm bij een opname.</summary>
        public const int MaxSuperSize = 8;

        /// <summary>Instellingen voor vrij rondkijken: bijna horizontaal tot recht omlaag, heel dichtbij, rustiger en zachter.</summary>
        public static CameraSettings FreeLook(CameraSettings normal)
        {
            normal.MinPitch = 1f;
            normal.MaxPitch = 89.9f;
            normal.MinDistance = math.min(normal.MinDistance, 3f);
            normal.TerrainClearance = math.min(normal.TerrainClearance, 2f);
            normal.RotateSpeed *= 0.5f;
            normal.TiltSpeed *= 0.5f;
            normal.MouseOrbitSensitivity *= 0.5f;
            normal.SmoothingHalfLife = math.max(normal.SmoothingHalfLife, 0.15f);
            normal.EdgeScroll = false;
            return normal;
        }

        /// <summary>
        /// Vergrotingsfactor zodat de opname minstens <paramref name="targetWidth"/> pixels breed wordt, zonder dat een
        /// zijde <see cref="MaxSide"/> of de factor <see cref="MaxSuperSize"/> overschrijdt.
        /// </summary>
        public static int SuperSize(int screenWidth, int screenHeight, int targetWidth)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(screenWidth), "Schermgrootte moet positief zijn.");
            }

            var factor = math.clamp((int)math.ceil(targetWidth / (float)screenWidth), 1, MaxSuperSize);
            while (factor > 1 && (long)math.max(screenWidth, screenHeight) * factor > MaxSide)
            {
                factor--;
            }

            return factor;
        }

        /// <summary>Bestandsnaam met tijdstip, bijvoorbeeld <c>stad-20261103-141500.png</c>.</summary>
        public static string FileName(DateTime moment) =>
            "stad-" + moment.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png";
    }
}
