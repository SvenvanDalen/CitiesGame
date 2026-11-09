using System.Collections.Generic;

namespace Cities.Gameplay.Tools
{
    /// <summary>Kiest het volgende of vorige wegtype uit een lijst, zodat een toets het type kan wisselen terwijl je tekent (E27-06).</summary>
    public static class RoadTypeCycle
    {
        /// <summary>Het type <paramref name="step"/> plaatsen na <paramref name="current"/> (met omslag); <c>null</c> als de lijst leeg is. Een onbekend huidig type begint vooraan.</summary>
        public static string Next(IReadOnlyList<string> ids, string current, int step)
        {
            if (ids == null || ids.Count == 0)
            {
                return null;
            }

            var index = -1;
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == current)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return ids[0];
            }

            var next = ((index + step) % ids.Count + ids.Count) % ids.Count;
            return ids[next];
        }
    }
}
