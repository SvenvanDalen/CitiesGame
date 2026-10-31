using Cities.World.Buildings;
using Cities.World.Grid;

namespace Cities.Editor.Models
{
    internal enum RoofKind
    {
        Gable,
        Flat,
        Sawtooth,
    }

    /// <summary>Wat er op de begane grond van de voorgevel zit.</summary>
    internal enum GroundKind
    {
        /// <summary>Een voordeur met raam per woning.</summary>
        Houses,

        /// <summary>Een centrale entree met luifel (portiek of hal van een flat).</summary>
        Entrance,

        /// <summary>Een etalage over de hele breedte met reclamebord en luifel.</summary>
        Shop,

        /// <summary>Een glazen lobby met draaideur en luifel.</summary>
        Lobby,

        /// <summary>Overheaddeuren in de voorgevel.</summary>
        Hall,

        /// <summary>Een kantoordeur voor, een laadperron met overheaddeuren achter.</summary>
        Docks,
    }

    internal enum WindowKind
    {
        /// <summary>Losse ramen per travee.</summary>
        Punched,

        /// <summary>Doorlopende glasstroken met stijlen, zoals bij kantoren.</summary>
        Ribbon,

        /// <summary>Hoge lichtstroken onder de dakrand, zoals bij hallen.</summary>
        HighStrip,
    }

    /// <summary>De vorm van één gebouwvariant (E05-08); <see cref="BuildingBuilder"/> maakt er het model van.</summary>
    internal sealed class BuildingDesign
    {
        public string Name;
        public string Id;
        public string Title;
        public string Description;
        public BuildingSpec Spec;

        /// <summary>Kavel in cellen van 8 m: breed langs de weg, diep vanaf de weg.</summary>
        public int Width;
        public int Depth;
        public ZoneDensity Density;
        public int Days;

        public int Floors = 1;
        public float FloorHeight = 3f;
        public float GroundHeight = 3f;

        /// <summary>Materiaalplek van de gevel (<see cref="BuildingMaterials.Slots"/>).</summary>
        public int Wall = BuildingMaterials.Slots.BrickRed;

        /// <summary>Kleur van reclame en luifel (AccentRed, AccentBlue of AccentGreen).</summary>
        public int Accent = BuildingMaterials.Slots.AccentRed;

        public RoofKind Roof = RoofKind.Flat;
        public GroundKind Ground = GroundKind.Entrance;
        public WindowKind Windows = WindowKind.Punched;

        /// <summary>Aantal woningen naast elkaar langs de straat (alleen <see cref="GroundKind.Houses"/>).</summary>
        public int Units = 1;

        public bool Balconies;
        public bool Chimneys;

        /// <summary>Ruimte achter het gebouw in meters, bijvoorbeeld voor silo's.</summary>
        public float Rear = 1f;

        public int Silos;
        public float SiloRadius = 2.6f;
        public float SiloHeight = 14f;
        public bool Stack;

        public ZoneType Zone => Spec.Zone;

        public bool HighDensity => Spec.Density == ZoneDensity.High;
    }
}
