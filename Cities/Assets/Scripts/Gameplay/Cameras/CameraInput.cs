using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Invoer van één frame voor de <see cref="CameraController"/>, los van het invoersysteem.</summary>
    public struct CameraInput
    {
        /// <summary>Pannen met de toetsen: x = rechts, y = vooruit, elk tussen -1 en 1.</summary>
        public float2 Move { get; set; }

        /// <summary>Draaien met de toetsen: positief is met de klok mee (van boven gezien).</summary>
        public float Rotate { get; set; }

        /// <summary>Kantelen met de toetsen: positief is steiler naar beneden kijken.</summary>
        public float Tilt { get; set; }

        /// <summary>Zoomen met de toetsen: positief is dichterbij.</summary>
        public float Zoom { get; set; }

        /// <summary>Scrollstappen dit frame; positief is dichterbij.</summary>
        public float ScrollSteps { get; set; }

        /// <summary>Muisverplaatsing in pixels terwijl de pan-knop (middelste muisknop) is ingedrukt.</summary>
        public float2 DragPanPixels { get; set; }

        /// <summary>Muisverplaatsing in pixels terwijl de draai-knop (rechtermuisknop) is ingedrukt.</summary>
        public float2 OrbitPixels { get; set; }

        /// <summary>Shift: sneller pannen, draaien en zoomen.</summary>
        public bool Fast { get; set; }

        /// <summary>Muispositie in pixels, (0, 0) linksonder; voor randscrollen.</summary>
        public float2 PointerPosition { get; set; }

        /// <summary>Schermgrootte in pixels; 0 schakelt randscrollen en slepen uit.</summary>
        public float2 ScreenSize { get; set; }
    }
}
