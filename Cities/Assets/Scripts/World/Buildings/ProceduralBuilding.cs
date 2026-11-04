using System;
using System.Collections.Generic;
using Cities.Core.Randomness;

namespace Cities.World.Buildings
{
    public enum BuildingModuleKind
    {
        /// <summary>Blinde muur.</summary>
        Wall,

        /// <summary>Muur met raam.</summary>
        Window,

        /// <summary>Voordeur, alleen op de begane grond aan de voorkant (lokaal -Z).</summary>
        Door,

        /// <summary>Hoekkolom waar twee gevels samenkomen.</summary>
        Corner,

        /// <summary>Plat dak.</summary>
        FlatRoof,

        /// <summary>Opstaande rand rond een plat dak.</summary>
        Parapet,

        /// <summary>Dakvlak van een zadeldak.</summary>
        RoofSlope,

        /// <summary>Driehoekige gevelpunt van een zadeldak.</summary>
        Gable,
    }

    public enum RoofStyle
    {
        Flat,
        Gabled,
    }

    /// <summary>Eén plek van één bouwdeel; coördinaten in meters, lokaal, met de oorsprong op de grond in het midden van de kavel.</summary>
    public readonly struct BuildingModule
    {
        public BuildingModule(BuildingModuleKind kind, int floor, float x, float y, float z, float yawDegrees, float width, float height, float depth = 0f)
        {
            Kind = kind;
            Floor = floor;
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
            Width = width;
            Height = height;
            Depth = depth;
        }

        public BuildingModuleKind Kind { get; }

        /// <summary>Verdieping vanaf 0; -1 voor het dak.</summary>
        public int Floor { get; }

        /// <summary>Middelpunt van het deel.</summary>
        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        /// <summary>Draaiing om de Y-as; bij 0 kijkt de buitenkant van het deel naar +Z, bij 180 naar -Z (de voorkant).</summary>
        public float YawDegrees { get; }

        public float Width { get; }

        public float Height { get; }

        /// <summary>Diepte, bij dakdelen de afmeting in de richting van de helling of de dikte.</summary>
        public float Depth { get; }
    }

    /// <summary>De gewenste afmetingen en stijl van een procedureel gebouw (E21-06).</summary>
    public readonly struct ProceduralBuildingSpec
    {
        public ProceduralBuildingSpec(int footprintWidth, int footprintDepth, float cellSize, int floors, RoofStyle roof, ulong seed,
            float floorHeight = 3f, float moduleWidth = 2.5f, float windowChance = 0.75f)
        {
            FootprintWidth = footprintWidth;
            FootprintDepth = footprintDepth;
            CellSize = cellSize;
            Floors = floors;
            Roof = roof;
            Seed = seed;
            FloorHeight = floorHeight;
            ModuleWidth = moduleWidth;
            WindowChance = windowChance;
        }

        public int FootprintWidth { get; }

        public int FootprintDepth { get; }

        public float CellSize { get; }

        public int Floors { get; }

        public RoofStyle Roof { get; }

        public ulong Seed { get; }

        public float FloorHeight { get; }

        /// <summary>Gewenste breedte van een gevelstuk; de echte breedte wordt zo gekozen dat de gevel er precies in past.</summary>
        public float ModuleWidth { get; }

        /// <summary>Kans (0 tot 1) dat een gevelstuk een raam heeft.</summary>
        public float WindowChance { get; }
    }

    /// <summary>
    /// Bouwt uit een footprint een gebouw van modulaire delen (E21-06): gevelstukken met of zonder raam, een deur aan de
    /// voorkant, hoekkolommen en een plat of zadeldak. De uitkomst is een lijst plaatsingen, zodat de vorm zonder Unity te
    /// testen is; <see cref="ProceduralBuildingMesh"/> maakt er geometrie van. Dezelfde invoer en seed geven hetzelfde
    /// gebouw.
    /// </summary>
    public static class ProceduralBuilding
    {
        /// <summary>Breedte van een hoekkolom.</summary>
        public const float CornerSize = 0.6f;

        /// <summary>Hoogte van de opstaande rand van een plat dak.</summary>
        public const float ParapetHeight = 0.5f;

        /// <summary>Dikte van een plat dak.</summary>
        public const float RoofThickness = 0.3f;

        /// <summary>Helling van een zadeldak in graden.</summary>
        public const float RoofPitchDegrees = 35f;

        public const int MaxFloors = 40;

        // De buitenkant van elke zijde: lokaal midden, de looprichting langs de zijde en de draaiing van de modules.
        private static readonly (float NormalX, float NormalZ, float AlongX, float AlongZ, float Yaw)[] Sides =
        {
            (0f, -1f, 1f, 0f, 180f),
            (1f, 0f, 0f, 1f, 90f),
            (0f, 1f, -1f, 0f, 0f),
            (-1f, 0f, 0f, -1f, 270f),
        };

        public static List<BuildingModule> Build(ProceduralBuildingSpec spec)
        {
            Validate(spec);
            var modules = new List<BuildingModule>();
            var halfX = spec.FootprintWidth * spec.CellSize * 0.5f;
            var halfZ = spec.FootprintDepth * spec.CellSize * 0.5f;
            var wallHeight = spec.Floors * spec.FloorHeight;

            for (var floor = 0; floor < spec.Floors; floor++)
            {
                var y = (floor + 0.5f) * spec.FloorHeight;
                AddCorners(modules, floor, y, halfX, halfZ, spec.FloorHeight);
                for (var side = 0; side < Sides.Length; side++)
                {
                    AddFacade(modules, spec, floor, y, side, halfX, halfZ);
                }
            }

            if (spec.Roof == RoofStyle.Flat)
            {
                AddFlatRoof(modules, halfX, halfZ, wallHeight);
            }
            else
            {
                AddGabledRoof(modules, halfX, halfZ, wallHeight);
            }

            return modules;
        }

        /// <summary>Totale hoogte van het gebouw inclusief dak.</summary>
        public static float Height(ProceduralBuildingSpec spec)
        {
            Validate(spec);
            var wall = spec.Floors * spec.FloorHeight;
            if (spec.Roof == RoofStyle.Flat)
            {
                return wall + RoofThickness + ParapetHeight;
            }

            var ridgeAcross = Math.Min(spec.FootprintWidth, spec.FootprintDepth) * spec.CellSize * 0.5f;
            return wall + (ridgeAcross * (float)Math.Tan(RoofPitchDegrees * Math.PI / 180d));
        }

        private static void Validate(ProceduralBuildingSpec spec)
        {
            if (spec.FootprintWidth < 1 || spec.FootprintDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "De footprint heeft minstens één cel per kant.");
            }

            if (!(spec.CellSize > 0f) || !(spec.FloorHeight > 0f) || !(spec.ModuleWidth > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "Celgrootte, verdiepingshoogte en modulebreedte moeten positief zijn.");
            }

            if (spec.Floors < 1 || spec.Floors > MaxFloors)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), spec.Floors, $"Een gebouw heeft 1 tot {MaxFloors} verdiepingen.");
            }

            if (spec.FootprintWidth * spec.CellSize < (2f * CornerSize) + 0.5f || spec.FootprintDepth * spec.CellSize < (2f * CornerSize) + 0.5f)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "De footprint is te klein voor hoekkolommen en een gevelstuk.");
            }
        }

        private static void AddCorners(List<BuildingModule> modules, int floor, float y, float halfX, float halfZ, float floorHeight)
        {
            var yaws = new[] { 180f, 90f, 0f, 270f };
            var xs = new[] { halfX, halfX, -halfX, -halfX };
            var zs = new[] { -halfZ, halfZ, halfZ, -halfZ };
            for (var i = 0; i < 4; i++)
            {
                modules.Add(new BuildingModule(BuildingModuleKind.Corner, floor, xs[i] - (Math.Sign(xs[i]) * CornerSize * 0.5f), y, zs[i] - (Math.Sign(zs[i]) * CornerSize * 0.5f), yaws[i], CornerSize, floorHeight, CornerSize));
            }
        }

        private static void AddFacade(List<BuildingModule> modules, ProceduralBuildingSpec spec, int floor, float y, int side, float halfX, float halfZ)
        {
            var (normalX, normalZ, alongX, alongZ, yaw) = Sides[side];
            var alongHalf = alongX != 0f ? halfX : halfZ;
            var length = (alongHalf * 2f) - (2f * CornerSize);
            var count = Math.Max(1, (int)Math.Round(length / spec.ModuleWidth));
            var width = length / count;
            var offsetToSide = normalX != 0f ? halfX : halfZ;
            var doorIndex = side == 0 && floor == 0 ? count / 2 : -1;
            for (var i = 0; i < count; i++)
            {
                var along = -alongHalf + CornerSize + ((i + 0.5f) * width);
                var x = (normalX * (offsetToSide - (CornerSize * 0.5f))) + (alongX * along);
                var z = (normalZ * (offsetToSide - (CornerSize * 0.5f))) + (alongZ * along);
                var kind = i == doorIndex ? BuildingModuleKind.Door : PickKind(spec, floor, side, i);
                modules.Add(new BuildingModule(kind, floor, x, y, z, yaw, width, spec.FloorHeight, CornerSize));
            }
        }

        private static BuildingModuleKind PickKind(ProceduralBuildingSpec spec, int floor, int side, int index)
        {
            var hash = SeedHash.Combine(spec.Seed, (ulong)floor, ((ulong)side << 20) | (ulong)index);
            var roll = (hash % 1000UL) / 1000f;
            return roll < spec.WindowChance ? BuildingModuleKind.Window : BuildingModuleKind.Wall;
        }

        private static void AddFlatRoof(List<BuildingModule> modules, float halfX, float halfZ, float wallHeight)
        {
            modules.Add(new BuildingModule(BuildingModuleKind.FlatRoof, -1, 0f, wallHeight + (RoofThickness * 0.5f), 0f, 0f, halfX * 2f, RoofThickness, halfZ * 2f));
            var y = wallHeight + RoofThickness + (ParapetHeight * 0.5f);
            for (var side = 0; side < Sides.Length; side++)
            {
                var (normalX, normalZ, alongX, _, yaw) = Sides[side];
                var length = alongX != 0f ? halfX * 2f : halfZ * 2f;
                var offset = normalX != 0f ? halfX : halfZ;
                modules.Add(new BuildingModule(BuildingModuleKind.Parapet, -1, normalX * offset, y, normalZ * offset, yaw, length, ParapetHeight, 0.2f));
            }
        }

        private static void AddGabledRoof(List<BuildingModule> modules, float halfX, float halfZ, float wallHeight)
        {
            // De nok loopt langs de langste zijde; de helling gaat over de korte zijde.
            var ridgeAlongX = halfX >= halfZ;
            var halfSpan = ridgeAlongX ? halfZ : halfX;
            var halfLength = ridgeAlongX ? halfX : halfZ;
            var pitch = RoofPitchDegrees * (float)Math.PI / 180f;
            var rise = halfSpan * (float)Math.Tan(pitch);
            var slope = halfSpan / (float)Math.Cos(pitch);
            var midY = wallHeight + (rise * 0.5f);
            for (var s = -1; s <= 1; s += 2)
            {
                var along = s * halfSpan * 0.5f;
                var yaw = ridgeAlongX ? (s < 0 ? 180f : 0f) : (s < 0 ? 270f : 90f);
                modules.Add(new BuildingModule(
                    BuildingModuleKind.RoofSlope,
                    -1,
                    ridgeAlongX ? 0f : along,
                    midY,
                    ridgeAlongX ? along : 0f,
                    yaw,
                    halfLength * 2f,
                    slope,
                    RoofThickness));
                var gableYaw = ridgeAlongX ? (s < 0 ? 270f : 90f) : (s < 0 ? 180f : 0f);
                modules.Add(new BuildingModule(
                    BuildingModuleKind.Gable,
                    -1,
                    ridgeAlongX ? s * halfLength : 0f,
                    wallHeight + (rise / 3f),
                    ridgeAlongX ? 0f : s * halfLength,
                    gableYaw,
                    halfSpan * 2f,
                    rise,
                    RoofThickness));
            }
        }
    }
}
