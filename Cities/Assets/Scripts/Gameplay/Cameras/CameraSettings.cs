using System;
using UnityEngine;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Gevoeligheden en grenzen van de stadscamera.</summary>
    [Serializable]
    public struct CameraSettings
    {
        [Tooltip("Panssnelheid met de toetsen, als fractie van de camera-afstand per seconde; zo voelt pannen op elke hoogte gelijk.")]
        [Min(0.01f)]
        public float PanSpeed;

        [Tooltip("Vermenigvuldiger met Shift ingedrukt.")]
        [Min(1f)]
        public float FastMultiplier;

        [Tooltip("Draaisnelheid met de toetsen in graden per seconde.")]
        public float RotateSpeed;

        [Tooltip("Kantelsnelheid met de toetsen in graden per seconde.")]
        public float TiltSpeed;

        [Tooltip("Graden per pixel muisbeweging bij draaien en kantelen met de rechtermuisknop.")]
        public float MouseOrbitSensitivity;

        [Tooltip("Fractie waarmee de afstand per scrollstap verandert.")]
        [Range(0.01f, 0.5f)]
        public float ZoomStep;

        [Tooltip("Zoomsnelheid met de toetsen, als fractie van de afstand per seconde.")]
        public float KeyZoomSpeed;

        [Min(1f)]
        public float MinDistance;

        public float MaxDistance;

        [Range(1f, 89.9f)]
        public float MinPitch;

        [Range(1f, 89.9f)]
        public float MaxPitch;

        [Tooltip("Tijd in seconden waarin de camera de helft van de resterende afstand naar het doel aflegt; 0 is direct.")]
        [Min(0f)]
        public float SmoothingHalfLife;

        [Tooltip("Pannen door de muis tegen de schermrand te houden.")]
        public bool EdgeScroll;

        [Tooltip("Breedte van de randzone in pixels.")]
        [Min(1f)]
        public float EdgeScrollBorder;

        [Tooltip("Verticaal beeldveld van de camera in graden, voor slepen met de middelste muisknop.")]
        [Range(10f, 120f)]
        public float FieldOfView;

        public static CameraSettings Default => new CameraSettings
        {
            PanSpeed = 1.1f,
            FastMultiplier = 3f,
            RotateSpeed = 90f,
            TiltSpeed = 60f,
            MouseOrbitSensitivity = 0.25f,
            ZoomStep = 0.12f,
            KeyZoomSpeed = 1.5f,
            MinDistance = 20f,
            MaxDistance = 6000f,
            MinPitch = 12f,
            MaxPitch = 88f,
            SmoothingHalfLife = 0.06f,
            EdgeScroll = false,
            EdgeScrollBorder = 12f,
            FieldOfView = 45f,
        };
    }
}
