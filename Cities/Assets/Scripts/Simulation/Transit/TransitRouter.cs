using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Eén rit van een reis (E09-03): met <see cref="Line"/> van <see cref="From"/> naar <see cref="To"/>, inclusief wachttijd en overstapstraf.</summary>
    public readonly struct TransitLeg
    {
        public TransitLeg(int line, int from, int to, float time)
        {
            Line = line;
            From = from;
            To = to;
            Time = time;
        }

        public int Line { get; }

        public int From { get; }

        public int To { get; }

        /// <summary>Seconden voor deze rit: wachten op het voertuig, rijden en bij een overstap de straf.</summary>
        public float Time { get; }
    }

    /// <summary>Een gepland traject over één of meer lijnen. Een lege reis (zelfde halte) duurt nul seconden.</summary>
    public sealed class TransitJourney
    {
        public TransitJourney(IReadOnlyList<TransitLeg> legs)
        {
            Legs = legs ?? throw new ArgumentNullException(nameof(legs));
            Time = legs.Sum(l => l.Time);
        }

        public IReadOnlyList<TransitLeg> Legs { get; }

        public float Time { get; }

        public int Transfers => math.max(0, Legs.Count - 1);
    }

    /// <summary>
    /// Plant reizen over meerdere lijnen (E09-03). Aan elke rit wordt de gemiddelde wachttijd toegevoegd (de helft van de echte
    /// frequentie van de lijn, <see cref="TransitNetwork.EffectiveHeadway"/>) en bij elke overstap een vaste straf. Een lijn die geen
    /// ronde is rijdt heen en terug, een ronde alleen vooruit. shortcut: dijkstra over alle halte-paren per lijn, prima voor stadslijnen
    /// maar traag bij duizenden haltes; upgraden naar een lijn-halte-graaf als de planner per burger moet draaien.
    /// </summary>
    public static class TransitRouter
    {
        public const float DefaultTransferPenalty = 120f;

        /// <summary>Snelste reis van <paramref name="from"/> naar <paramref name="to"/> bij <paramref name="speed"/> (m/s), of null als er geen is.</summary>
        public static TransitJourney Plan(TransitNetwork network, int from, int to, float speed, float transferPenalty = DefaultTransferPenalty)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            if (!(speed > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "De snelheid moet groter dan nul zijn.");
            }

            if (!(transferPenalty >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(transferPenalty), transferPenalty, "De overstapstraf kan niet negatief zijn.");
            }

            if (!network.HasStop(from) || !network.HasStop(to))
            {
                throw new ArgumentException("Onbekende halte.");
            }

            var best = new Dictionary<int, float> { [from] = 0f };
            var via = new Dictionary<int, TransitLeg>();
            var open = new SortedSet<(float Cost, int Stop)> { (0f, from) };
            var waits = network.Lines.ToDictionary(l => l.Id, l => network.EffectiveHeadway(l.Id, speed) / 2f);
            while (open.Count > 0)
            {
                var (cost, stop) = open.Min;
                open.Remove(open.Min);
                if (stop == to)
                {
                    break;
                }

                var boarding = stop == from ? 0f : transferPenalty;
                foreach (var (line, next, distance) in Rides(network, stop))
                {
                    var total = cost + boarding + waits[line] + (distance / speed);
                    if (best.TryGetValue(next, out var known) && known <= total)
                    {
                        continue;
                    }

                    open.Remove((known, next));
                    best[next] = total;
                    via[next] = new TransitLeg(line, stop, next, total - cost);
                    open.Add((total, next));
                }
            }

            return best.ContainsKey(to) ? new TransitJourney(Trace(via, from, to)) : null;
        }

        private static List<TransitLeg> Trace(Dictionary<int, TransitLeg> via, int from, int to)
        {
            var legs = new List<TransitLeg>();
            for (var at = to; at != from; at = legs[legs.Count - 1].From)
            {
                legs.Add(via[at]);
            }

            legs.Reverse();
            return legs;
        }

        /// <summary>Alle haltes die vanaf <paramref name="stop"/> zonder overstap te bereiken zijn, met de afstand over de lijn.</summary>
        private static IEnumerable<(int Line, int Stop, float Distance)> Rides(TransitNetwork network, int stop)
        {
            foreach (var line in network.Lines)
            {
                for (var i = 0; i < line.Stops.Count; i++)
                {
                    if (line.Stops[i] != stop)
                    {
                        continue;
                    }

                    foreach (var step in line.IsLoop ? new[] { 1 } : new[] { 1, -1 })
                    {
                        foreach (var (reached, distance) in Along(network, line, i, step))
                        {
                            yield return (line.Id, reached, distance);
                        }
                    }
                }
            }
        }

        private static IEnumerable<(int Stop, float Distance)> Along(TransitNetwork network, TransitLine line, int start, int step)
        {
            var count = line.Stops.Count;
            var distance = 0f;
            var at = start;
            for (var k = 1; k < count; k++)
            {
                var next = at + step;
                if (line.IsLoop)
                {
                    next %= count;
                }
                else if (next < 0 || next >= count)
                {
                    yield break;
                }

                distance += math.distance(network.PositionOf(line.Stops[at]), network.PositionOf(line.Stops[next]));
                at = next;
                yield return (line.Stops[next], distance);
            }
        }
    }
}
