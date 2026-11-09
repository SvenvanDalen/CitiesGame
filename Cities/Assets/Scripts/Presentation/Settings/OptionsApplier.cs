using Cities.Core.Settings;
using Cities.Gameplay.Cameras;
using Cities.Gameplay.Visuals;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.Settings
{
    /// <summary>Voert de <see cref="UserSettings"/> door naar Unity: schermmodus, vsync, kwaliteit, hoofdvolume, interfaceschaal en camera (E27-12).</summary>
    public static class OptionsApplier
    {
        public static void Apply(UserSettings settings, PanelSettings panel, CityCameraBehaviour camera)
        {
            Screen.fullScreen = settings.GetBool(SettingsCatalog.Fullscreen);
            QualitySettings.vSyncCount = settings.GetBool(SettingsCatalog.VSync) ? 1 : 0;

            var quality = new QualityManager();
            quality.Apply(settings);
            QualitySettings.shadowDistance = quality.Profile.ShadowDistance;

            AudioListener.volume = settings.Get(SettingsCatalog.VolumeMaster);
            if (panel != null)
            {
                panel.scale = UiScaling.Effective(settings, Screen.width, Screen.height, Screen.dpi);
            }

            if (camera != null)
            {
                camera.ApplyUserSettings(settings);
            }
        }
    }
}
