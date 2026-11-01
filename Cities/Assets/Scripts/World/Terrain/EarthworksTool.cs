using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Koppelt het terreinpenseel aan grondverzet: <see cref="Preview"/> meet hoeveel grond een streek verplaatst zonder de kaart te
    /// veranderen, <see cref="Execute"/> voert de streek alleen uit als het depot het toelaat. De kosten boekt de aanroeper.
    /// </summary>
    public static class EarthworksTool
    {
        public static EarthworksQuote Preview(WorldMap map, TerraformMode mode, float2 center, float radius, float step, float target, SoilDepot depot)
        {
            var corners = TerrainBrush.CornersUnder(map, center, radius);
            var before = Copy(map, corners);
            var changed = TerrainBrush.Apply(map, mode, center, radius, step, target);
            var quote = Measure(map, changed, corners, before, depot);
            Restore(map, corners, before);
            return quote;
        }

        public static EarthworksQuote Execute(WorldMap map, TerraformMode mode, float2 center, float radius, float step, float target, SoilDepot depot)
        {
            var quote = Preview(map, mode, center, radius, step, target, depot);
            if (quote.Feasible && quote.Cost > 0f)
            {
                map.NotifyHeightChanged(TerrainBrush.Apply(map, mode, center, radius, step, target));
                Earthworks.Commit(quote, depot);
            }

            return quote;
        }

        private static EarthworksQuote Measure(WorldMap map, RectInt changed, RectInt corners, float[] before, SoilDepot depot)
        {
            var area = map.Dimensions.CellSize * map.Dimensions.CellSize;
            float cut = 0f, fill = 0f;
            for (var z = changed.yMin; z < changed.yMax; z++)
            {
                for (var x = changed.xMin; x < changed.xMax; x++)
                {
                    var delta = map.Height[x, z] - before[((z - corners.yMin) * corners.width) + (x - corners.xMin)];
                    cut += math.max(-delta, 0f) * area;
                    fill += math.max(delta, 0f) * area;
                }
            }

            return Earthworks.Quote(cut, fill, depot);
        }

        private static float[] Copy(WorldMap map, RectInt corners)
        {
            var values = new float[corners.width * corners.height];
            for (var z = 0; z < corners.height; z++)
            {
                for (var x = 0; x < corners.width; x++)
                {
                    values[(z * corners.width) + x] = map.Height[corners.xMin + x, corners.yMin + z];
                }
            }

            return values;
        }

        private static void Restore(WorldMap map, RectInt corners, float[] values)
        {
            for (var z = 0; z < corners.height; z++)
            {
                for (var x = 0; x < corners.width; x++)
                {
                    map.Height[corners.xMin + x, corners.yMin + z] = values[(z * corners.width) + x];
                }
            }
        }
    }
}
