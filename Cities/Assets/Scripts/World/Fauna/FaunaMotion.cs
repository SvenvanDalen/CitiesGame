using System;
using Unity.Mathematics;

namespace Cities.World.Fauna
{
    /// <summary>
    /// Rekent de stand van een dier uit tijd en groep, zonder toestand: dieren kosten dus geen simulatietijd en
    /// hoeven niet opgeslagen te worden. Een dier dat op een plek zou komen waar het niet kan (vis op het droge, hert
    /// in het water) is er op dat moment gewoon niet.
    /// </summary>
    public static class FaunaMotion
    {
        private const float HeadingLookAhead = 0.25f;
        private const float MinimumFishDepth = 1.2f;
        private const float TwoPi = 6.2831855f;

        /// <summary>Vleugelslagen per seconde van een meeuw of kraai.</summary>
        private const float FlapRate = 3f;

        public static bool TryPose(in FaunaGroup group, int member, float time, IFaunaHabitat habitat, out FaunaPose pose)
        {
            if (habitat == null)
            {
                throw new ArgumentNullException(nameof(habitat));
            }

            pose = default;
            if ((uint)member >= (uint)group.Count)
            {
                return false;
            }

            var ground = Path(group, member, time);
            if (!habitat.InBounds(ground))
            {
                return false;
            }

            var direction = Path(group, member, time + HeadingLookAhead) - ground;
            var yaw = math.lengthsq(direction) > 1e-8f ? math.atan2(direction.x, direction.y) : Unit(group, member, 20) * TwoPi;
            var terrain = habitat.GroundHeight(ground);
            var depth = habitat.WaterDepth(ground);
            switch (group.Kind)
            {
                case FaunaKind.Birds:
                    pose = Bird(group, member, time, ground, yaw, terrain + depth);
                    return true;
                case FaunaKind.Fish:
                    if (depth < MinimumFishDepth)
                    {
                        return false;
                    }

                    // De vis zwemt tussen de bodem en de waterspiegel en kronkelt met zijn staart.
                    var below = math.min(0.4f + (Unit(group, member, 21) * 1.6f), depth - 0.3f);
                    var wobble = math.sin((time * 7f) + (Unit(group, member, 22) * TwoPi)) * 0.12f;
                    pose = new FaunaPose(new float3(ground.x, terrain + depth - below, ground.y), yaw + wobble, 0f, 0f, 0f);
                    return true;
                default:
                    if (depth > 0f || !habitat.IsOpenLand(ground))
                    {
                        return false;
                    }

                    // De neus volgt de helling over de lengte van het dier.
                    var forward = new float2(math.sin(yaw), math.cos(yaw));
                    var rise = habitat.GroundHeight(ground + forward) - habitat.GroundHeight(ground - forward);
                    pose = new FaunaPose(new float3(ground.x, terrain, ground.y), yaw, math.atan2(rise, 2f), 0f, 0f);
                    return true;
            }
        }

        /// <summary>Plek van een dier op de grond (x, z) op een tijdstip.</summary>
        public static float2 Path(in FaunaGroup group, int member, float time)
        {
            var lag = Unit(group, member, 10);
            var phase = Unit(group, 0, 11) * TwoPi;
            var sign = Direction(group);
            switch (group.Kind)
            {
                case FaunaKind.Birds:
                {
                    // Cirkel; elk dier ligt een eindje voor of achter de rest en wat dichter of verder van het midden.
                    var angle = phase + (sign * ((time * group.Speed / group.Radius) - (lag * 0.4f)));
                    var radius = group.Radius + ((Unit(group, member, 13) - 0.5f) * 16f);
                    return group.Anchor + (radius * new float2(math.cos(angle), math.sin(angle)));
                }

                case FaunaKind.Fish:
                {
                    // Ovaal; de school houdt samen met een kleine afwijking per vis.
                    var angle = phase + (sign * ((time * group.Speed / group.Radius) - (lag * 0.15f)));
                    var spread = (new float2(Unit(group, member, 13), Unit(group, member, 14)) - 0.5f) * 3f;
                    return group.Anchor + new float2(math.cos(angle) * group.Radius, math.sin(angle) * group.Radius * 0.6f) + spread;
                }

                default:
                {
                    // Rustig dwalen over een weitje; twee trage golven met verschillend ritme geven geen vaste baan.
                    var w = group.Speed / group.Radius;
                    var center = group.Anchor + new float2(math.sin((time * w) + phase) * group.Radius, math.cos((time * w * 0.7f) + (phase * 2f)) * group.Radius * 0.8f);
                    var offset = (new float2(Unit(group, member, 13), Unit(group, member, 14)) - 0.5f) * 10f;
                    var own = new float2(math.sin((time * 0.15f) + (lag * TwoPi)), math.cos((time * 0.11f) + (lag * TwoPi))) * 2f;
                    return center + offset + own;
                }
            }
        }

        /// <summary>+1 loopt de baan tegen de klok in (gezien van boven met x naar rechts), -1 met de klok mee.</summary>
        private static float Direction(in FaunaGroup group) => Unit(group, 0, 12) < 0.5f ? -1f : 1f;

        private static FaunaPose Bird(in FaunaGroup group, int member, float time, float2 ground, float yaw, float surface)
        {
            var altitude = group.Height + ((Unit(group, member, 15) - 0.5f) * 10f) + (math.sin((time * 0.5f) + (Unit(group, member, 16) * TwoPi)) * 1.5f);
            var flapPhase = (time * FlapRate * TwoPi) + (Unit(group, member, 17) * TwoPi);

            // Om beurten zweven en klapwieken, per vogel in een eigen ritme.
            var gliding = math.sin((time * 0.4f) + (Unit(group, member, 18) * TwoPi)) > 0.2f;
            var flap = gliding ? 0.15f : math.sin(flapPhase) * 0.65f;

            // Naar de binnenkant van de bocht gekanteld.
            return new FaunaPose(new float3(ground.x, surface + altitude, ground.y), yaw, 0f, -Direction(group) * 0.3f, flap);
        }

        private static float Unit(in FaunaGroup group, int member, int salt) => FaunaGroups.Unit(group.Seed, (member * 64) + salt);
    }
}
