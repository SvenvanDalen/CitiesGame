using System;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Zet bij het starten vaste stukken akker of bebouwde grond op de kaart, voor testscenes tot boerderijen,
    /// gebouwen en wegen dat zelf doen (E05, E06).
    /// </summary>
    public sealed class GroundCoverPainterBehaviour : MonoBehaviour
    {
        [Serializable]
        public struct Area
        {
            [Tooltip("Cellen (x, z, breedte, diepte).")]
            public RectInt Cells;

            public GroundCover Cover;
        }

        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private Area[] _areas = Array.Empty<Area>();

        private void Start()
        {
            var map = _world.Map;
            foreach (var area in _areas)
            {
                var cells = area.Cells;
                cells.ClampToBounds(map.AllCells);
                for (var z = cells.yMin; z < cells.yMax; z++)
                {
                    for (var x = cells.xMin; x < cells.xMax; x++)
                    {
                        map.GroundCover[x, z] = area.Cover;
                    }
                }

                map.NotifyGroundCoverChanged(cells);
            }
        }
    }
}
