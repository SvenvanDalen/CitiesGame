using System.Collections.Generic;
using System.Linq;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Welke footprints al bezet zijn. shortcut: lineair zoeken over alle footprints, prima voor testscenes; vervang
    /// door een bezettingslaag op de WorldMap als gebouwen echt geplaatst worden (E05-07).
    /// </summary>
    public sealed class FootprintOccupancy
    {
        public const string Occupied = "Bezet";

        private readonly List<Footprint> _placed = new List<Footprint>();

        public IReadOnlyList<Footprint> Placed => _placed;

        public string Check(in Footprint footprint)
        {
            var candidate = footprint;
            return _placed.Any(placed => placed.Overlaps(candidate)) ? Occupied : null;
        }

        public void Add(in Footprint footprint) => _placed.Add(footprint);

        public void Clear() => _placed.Clear();
    }
}
