using System;
using Cities.World.Grid;

namespace Cities.World.Maps
{
    /// <summary>Zet een <see cref="MapDocument"/> over in de lagen van een <see cref="WorldMap"/> en omgekeerd (E21-07).</summary>
    public static class MapDocumentApplier
    {
        public static void Apply(MapDocument document, WorldMap map)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (document.Dimensions != map.Dimensions)
            {
                throw new ArgumentException($"Kaart van {document.Dimensions}, wereld van {map.Dimensions}.", nameof(document));
            }

            var heights = map.Height.Values;
            var water = map.WaterDepth.Values;
            for (var i = 0; i < document.Heights.Length; i++)
            {
                heights[i] = document.Heights[i];
                water[i] = document.WaterDepths[i];
            }

            map.NotifyHeightChanged(map.AllCorners);
        }

        public static MapDocument Capture(WorldMap map, string name)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var document = new MapDocument(map.Dimensions, name);
            for (var z = 0; z < document.CornersZ; z++)
            {
                for (var x = 0; x < document.CornersX; x++)
                {
                    document.SetHeight(x, z, map.Height[x, z]);
                    document.SetWaterDepth(x, z, map.WaterDepth[x, z]);
                }
            }

            return document;
        }
    }
}
