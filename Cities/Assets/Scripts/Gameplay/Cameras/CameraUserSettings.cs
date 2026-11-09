using System;
using Cities.Core.Settings;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Legt de camera-instellingen van de speler (gevoeligheid, omkeren, randscrollen) over de basisinstellingen van de scène (E27-09).</summary>
    public static class CameraUserSettings
    {
        public static CameraSettings Apply(CameraSettings baseline, UserSettings user)
        {
            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var scale = user.Get(SettingsCatalog.CameraSensitivity);
            baseline.MouseOrbitSensitivity *= scale;
            baseline.RotateSpeed *= scale;
            baseline.TiltSpeed *= scale;
            baseline.EdgeScroll = user.GetBool(SettingsCatalog.EdgeScroll);
            baseline.InvertZoom = user.GetBool(SettingsCatalog.InvertZoom);
            baseline.InvertOrbitY = user.GetBool(SettingsCatalog.InvertCameraY);
            return baseline;
        }
    }
}
