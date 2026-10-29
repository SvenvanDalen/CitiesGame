using System;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>
    /// Logica van de vrije stadscamera, los van Unity zodat ze volledig testbaar is. Invoer verschuift een doelstand;
    /// de zichtbare stand volgt die met exponentiële demping, onafhankelijk van de framerate.
    /// </summary>
    /// <remarks>
    /// Het draaipunt ligt altijd op het oppervlak. De camera blijft minstens
    /// <see cref="CameraSettings.TerrainClearance"/> boven terrein en water, door eerst steiler te kantelen en zo
    /// nodig verder weg te gaan, en komt niet hoger dan <see cref="CameraSettings.MaxHeight"/>. Dat geldt ook voor de
    /// gedempte stand, zodat de camera tijdens het bijsturen niet door een heuvel gaat.
    /// </remarks>
    public sealed class CameraController
    {
        private CameraSettings _settings;
        private float2 _boundsMin;
        private float2 _boundsMax;
        private ITerrainHeight _terrain;

        public CameraController(CameraSettings settings, CameraRig start, float2 boundsMin, float2 boundsMax, ITerrainHeight terrain = null)
        {
            if (math.any(boundsMax < boundsMin))
            {
                throw new ArgumentException("Bovengrens ligt onder de ondergrens.", nameof(boundsMax));
            }

            _settings = settings;
            _boundsMin = boundsMin;
            _boundsMax = boundsMax;
            _terrain = terrain ?? FlatTerrain.Zero;
            Target = Clamp(start);
            Current = Target;
        }

        /// <summary>Oppervlak waar de camera niet doorheen mag; te wisselen als een andere stad laadt.</summary>
        public ITerrainHeight Terrain
        {
            get => _terrain;
            set
            {
                _terrain = value ?? FlatTerrain.Zero;
                Target = Clamp(Target);
                Current = Clamp(Current);
            }
        }

        /// <summary>De stand waar de camera naartoe beweegt.</summary>
        public CameraRig Target { get; private set; }

        /// <summary>De zichtbare stand.</summary>
        public CameraRig Current { get; private set; }

        public CameraSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                Target = Clamp(Target);
            }
        }

        /// <summary>Verwerkt de invoer van één frame en laat de camera het doel volgen.</summary>
        public void Update(in CameraInput input, float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Frametijd moet 0 of groter zijn.");
            }

            var speed = input.Fast ? _settings.FastMultiplier : 1f;
            var target = Target;
            var pivot = target.Pivot;

            // Pannen: toetsen en randscrollen schalen met de afstand, slepen volgt de muis precies.
            var move = math.clamp(input.Move + EdgeScrollDirection(input), -1f, 1f);
            var panDistance = target.Distance * _settings.PanSpeed * speed * deltaSeconds;
            pivot += ((target.GroundRight * move.x) + (target.GroundForward * move.y)) * panDistance;
            pivot += DragPanOffset(target, input);

            var yaw = target.Yaw + (input.Rotate * _settings.RotateSpeed * speed * deltaSeconds) +
                      (input.OrbitPixels.x * _settings.MouseOrbitSensitivity);
            var pitch = target.Pitch + (input.Tilt * _settings.TiltSpeed * speed * deltaSeconds) -
                        (input.OrbitPixels.y * _settings.MouseOrbitSensitivity);

            // Zoomen werkt met factoren, zodat elke stap op elke hoogte even groot voelt.
            var zoomFactor = math.pow(1f - _settings.ZoomStep, input.ScrollSteps * speed) *
                             math.exp(-input.Zoom * _settings.KeyZoomSpeed * speed * deltaSeconds);
            var distance = ClampDistance(target.Distance * zoomFactor);

            // Zoomen naar de cursor: schalen rond het punt onder de muis houdt dat punt op dezelfde plek in beeld.
            if (_settings.ZoomToCursor && input.HasCursorPoint && math.abs(distance - target.Distance) > 1e-4f)
            {
                var scale = distance / target.Distance;
                pivot = input.CursorPoint + ((pivot - input.CursorPoint) * scale);
            }

            Target = Clamp(new CameraRig(pivot, CameraRig.NormalizeYaw(yaw), pitch, distance));
            Current = Clamp(Current.Lerp(Target, SmoothingFactor(deltaSeconds)));
        }

        /// <summary>Zet camera en doel direct op een stand, zonder demping; bijvoorbeeld bij laden of een bladwijzer.</summary>
        public void Teleport(CameraRig rig)
        {
            Target = Clamp(rig);
            Current = Target;
        }

        /// <summary>Verplaatst alleen het doel; de camera volgt gedempt.</summary>
        public void SetTarget(CameraRig rig) => Target = Clamp(rig);

        public void SetBounds(float2 boundsMin, float2 boundsMax)
        {
            if (math.any(boundsMax < boundsMin))
            {
                throw new ArgumentException("Bovengrens ligt onder de ondergrens.", nameof(boundsMax));
            }

            _boundsMin = boundsMin;
            _boundsMax = boundsMax;
            Target = Clamp(Target);
        }

        private float SmoothingFactor(float deltaSeconds) =>
            _settings.SmoothingHalfLife <= 0f ? 1f : 1f - math.exp2(-deltaSeconds / _settings.SmoothingHalfLife);

        private float2 EdgeScrollDirection(in CameraInput input)
        {
            if (!_settings.EdgeScroll || math.any(input.ScreenSize <= 0f))
            {
                return float2.zero;
            }

            var border = _settings.EdgeScrollBorder;
            var p = input.PointerPosition;
            if (math.any(p < 0f) || math.any(p > input.ScreenSize))
            {
                // Muis buiten het venster: niet scrollen, anders schuift de stad weg bij alt-tab.
                return float2.zero;
            }

            return new float2(EdgeAxis(p.x, input.ScreenSize.x, border), EdgeAxis(p.y, input.ScreenSize.y, border));
        }

        private static float EdgeAxis(float position, float size, float border)
        {
            if (position <= border)
            {
                return -1f;
            }

            return position >= size - border ? 1f : 0f;
        }

        /// <summary>
        /// Verschuiving waarmee de grond onder de muis meebeweegt met de muis (benadering in het brandpuntsvlak van het
        /// draaipunt): meters per pixel = 2 · afstand · tan(fov/2) / schermhoogte.
        /// </summary>
        private float3 DragPanOffset(in CameraRig rig, in CameraInput input)
        {
            if (math.all(input.DragPanPixels == 0f) || input.ScreenSize.y <= 0f)
            {
                return float3.zero;
            }

            var metersPerPixel = 2f * rig.Distance * math.tan(math.radians(_settings.FieldOfView) * 0.5f) / input.ScreenSize.y;

            // Kijkt de camera schuin, dan beslaat een pixel verticaal meer grond (1 / sin(pitch)).
            var forwardStretch = 1f / math.max(math.sin(math.radians(rig.Pitch)), 0.2f);
            return -((rig.GroundRight * input.DragPanPixels.x) + (rig.GroundForward * input.DragPanPixels.y * forwardStretch)) * metersPerPixel;
        }

        private float ClampDistance(float distance) =>
            math.clamp(distance, _settings.MinDistance, math.max(_settings.MaxDistance, _settings.MinDistance));

        private CameraRig Clamp(CameraRig rig)
        {
            var pivot = rig.Pivot;
            pivot.x = math.clamp(pivot.x, _boundsMin.x, _boundsMax.x);
            pivot.z = math.clamp(pivot.z, _boundsMin.y, _boundsMax.y);
            pivot.y = _terrain.SurfaceHeight(pivot.xz);
            var pitch = math.clamp(rig.Pitch, _settings.MinPitch, _settings.MaxPitch);
            var distance = ClampDistance(rig.Distance);

            // Niet hoger dan het maximum: dichterbij komen, maar niet dichter dan de minimale afstand.
            var maxRise = _settings.MaxHeight - pivot.y;
            var sinPitch = math.sin(math.radians(pitch));
            if (maxRise > 0f && distance * sinPitch > maxRise)
            {
                distance = math.max(maxRise / sinPitch, _settings.MinDistance);
            }

            var clamped = new CameraRig(pivot, CameraRig.NormalizeYaw(rig.Yaw), pitch, distance);
            return KeepAboveSurface(clamped);
        }

        /// <summary>
        /// Tilt eerst steiler zodat de camera boven het oppervlak blijft; is dat niet genoeg, dan gaat hij verder weg.
        /// Een paar herhalingen, omdat de camera daarbij boven ander terrein komt te hangen.
        /// </summary>
        private CameraRig KeepAboveSurface(CameraRig rig)
        {
            var maxSin = math.sin(math.radians(_settings.MaxPitch));
            for (var i = 0; i < 3; i++)
            {
                var position = rig.Position;
                var required = _terrain.SurfaceHeight(position.xz) + _settings.TerrainClearance - rig.Pivot.y;
                if (position.y - rig.Pivot.y >= required - 0.001f)
                {
                    break;
                }

                var neededSin = required / rig.Distance;
                rig = neededSin <= maxSin
                    ? new CameraRig(rig.Pivot, rig.Yaw, math.degrees(math.asin(neededSin)), rig.Distance)
                    : new CameraRig(rig.Pivot, rig.Yaw, _settings.MaxPitch, required / maxSin);
            }

            return rig;
        }
    }
}
