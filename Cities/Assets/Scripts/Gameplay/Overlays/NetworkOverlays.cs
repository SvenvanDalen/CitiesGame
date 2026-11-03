using System;
using System.Collections.Generic;
using Cities.Simulation.Utilities;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Een afvalopslag op een plek, voor de afvaloverlay.</summary>
    public readonly struct WasteSite
    {
        public WasteSite(ulong id, float2 position, WasteBin bin)
        {
            Id = id;
            Position = position;
            Bin = bin ?? throw new ArgumentNullException(nameof(bin));
        }

        public ulong Id { get; }

        public float2 Position { get; }

        public WasteBin Bin { get; }
    }

    /// <summary>Overlays van de nutsnetten en het afval (E17-04): stroom, water, riool en afval.</summary>
    public static class NetworkOverlays
    {
        public const string PowerId = "power";
        public const string WaterId = "water";
        public const string SewageId = "sewage";
        public const string GarbageId = "garbage";

        /// <summary>Breedte in meters van het vlak van een knoop.</summary>
        public const float NodeSize = 8f;

        /// <summary>Breedte in meters van een leiding.</summary>
        public const float PipeWidth = 2f;

        private const float Lift = 0.6f;

        public static IEntityOverlaySource Power(UtilityNetwork network, ITerrainHeight terrain, Func<long> version = null) =>
            Utility(PowerId, "Stroom", "Stroomvoorziening", network, terrain, version);

        public static IEntityOverlaySource Water(UtilityNetwork network, ITerrainHeight terrain, Func<long> version = null) =>
            Utility(WaterId, "Water", "Watervoorziening", network, terrain, version);

        public static IEntityOverlaySource Sewage(UtilityNetwork network, ITerrainHeight terrain, Func<long> version = null) =>
            Utility(SewageId, "Riool", "Rioolafvoer", network, terrain, version);

        /// <summary>
        /// Eén net: elke knoop is een vlak en elke leiding een lint, gekleurd naar het deel van de vraag dat wordt geleverd
        /// (rood niets, groen alles). Een leiding krijgt de slechtste van haar twee knopen.
        /// </summary>
        public static IEntityOverlaySource Utility(string id, string name, string title, UtilityNetwork network, ITerrainHeight terrain, Func<long> version = null)
        {
            if (network is null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            if (terrain is null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            var legend = new OverlayLegend(title, "%", 0f, 100f, OverlayColorScale.Good, "0");
            return new EntityOverlaySource(id, name, OverlayTarget.Segment, legend, output => CollectUtility(network, terrain, output), version);
        }

        /// <summary>Afvalopslagen als vlakken, gekleurd naar hun vulling (rood is vol).</summary>
        public static IEntityOverlaySource Garbage(Func<IEnumerable<WasteSite>> sites, ITerrainHeight terrain, Func<long> version = null)
        {
            if (sites is null)
            {
                throw new ArgumentNullException(nameof(sites));
            }

            if (terrain is null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }

            var legend = new OverlayLegend("Afvalopslag", "% vol", 0f, 100f, OverlayColorScale.Hot, "0");
            return new EntityOverlaySource(
                GarbageId,
                "Afval",
                OverlayTarget.Building,
                legend,
                output =>
                {
                    foreach (var site in sites())
                    {
                        var at = Point(terrain, site.Position);
                        output.Add(new OverlayEntityValue(site.Id, at, at, NodeSize, FillPercent(site.Bin)));
                    }
                },
                version);
        }

        /// <summary>Vulling van een opslag in procenten; een overvolle opslag blijft op 100.</summary>
        public static float FillPercent(WasteBin bin)
        {
            if (bin is null)
            {
                throw new ArgumentNullException(nameof(bin));
            }

            return math.clamp(bin.Level / bin.Capacity, 0f, 1f) * 100f;
        }

        private static void CollectUtility(UtilityNetwork network, ITerrainHeight terrain, List<OverlayEntityValue> output)
        {
            foreach (var node in network.Nodes)
            {
                var at = Point(terrain, network.PositionOf(node));
                var satisfaction = network.Satisfaction(node);
                output.Add(new OverlayEntityValue((ulong)node, at, at, NodeSize, satisfaction * 100f));
                foreach (var other in network.LinksOf(node))
                {
                    if (other <= node)
                    {
                        continue;
                    }

                    var worst = math.min(satisfaction, network.Satisfaction(other));
                    output.Add(new OverlayEntityValue(PairId(node, other), at, Point(terrain, network.PositionOf(other)), PipeWidth, worst * 100f));
                }
            }
        }

        /// <summary>ID van een leiding; de bovenste bit houdt hem uit de buurt van de knoop-ID's.</summary>
        private static ulong PairId(int a, int b) => PipeFlag | ((ulong)(uint)a << 32) | (uint)b;

        private const ulong PipeFlag = 1UL << 63;

        private static float3 Point(ITerrainHeight terrain, float2 position) =>
            new float3(position.x, terrain.SurfaceHeight(position) + Lift, position.y);
    }
}
