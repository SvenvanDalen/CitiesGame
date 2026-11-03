using System;
using Cities.Gameplay.Cameras;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.Gameplay.Cheats
{
    /// <summary>Zet de stadscamera direct boven een punt van de kaart, op de hoogte van het terrein.</summary>
    public sealed class CameraTeleporter : IViewTeleporter
    {
        private readonly CameraController _controller;

        public CameraTeleporter(CameraController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public string TeleportTo(float x, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z))
            {
                return "Ongeldige positie.";
            }

            var ground = _controller.Terrain.SurfaceHeight(new float2(x, z));
            _controller.Teleport(_controller.Target.WithPivot(new float3(x, ground, z)));
            return null;
        }
    }
}
