using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Het ondergrondse leidingnet (E06-05): bronnen (pompstation, gemaal) voeden vragers via buizen met een beperkte capaciteit.
    /// <see cref="Solve"/> stuurt het water langs de kortste route vanaf de bronnen; een te dunne buis of te kleine bron geeft alle
    /// vragers erachter hetzelfde tekort. De belasting per buis (<see cref="Load"/>) voedt de kleur in de ondergrond-weergave.
    /// </summary>
    // shortcut: water stroomt langs een kortste-routeboom vanaf de bronnen; lussen worden niet benut als tweede route. Voor echte maaswerken een maximale stroom.
    public sealed class PipeNetwork
    {
        private readonly List<float> _supply = new List<float>();
        private readonly List<float> _demand = new List<float>();
        private readonly List<(int A, int B, float Capacity)> _pipes = new List<(int, int, float)>();
        private float[] _satisfaction = Array.Empty<float>();
        private float[] _load = Array.Empty<float>();

        /// <summary>Voegt een knoop toe met aanbod (bron) en/of vraag.</summary>
        public int AddNode(float supply, float demand)
        {
            if (!(supply >= 0f) || !(demand >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(supply), "Aanbod en vraag moeten nul of meer zijn.");
            }

            _supply.Add(supply);
            _demand.Add(demand);
            return _supply.Count - 1;
        }

        public int AddPipe(int a, int b, float capacity)
        {
            if (a < 0 || b < 0 || a >= _supply.Count || b >= _supply.Count || a == b)
            {
                throw new ArgumentException("Een buis verbindt twee verschillende bestaande knopen.");
            }

            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De buiscapaciteit moet groter dan nul zijn.");
            }

            _pipes.Add((a, b, capacity));
            return _pipes.Count - 1;
        }

        /// <summary>Het deel van zijn vraag dat de knoop krijgt (0 tot 1); knopen zonder route naar een bron krijgen 0.</summary>
        public float Satisfaction(int node) => _satisfaction[node];

        /// <summary>Stroom door de buis gedeeld door de capaciteit; boven 1 is de buis overbelast (zijn vraag wordt dan teruggeschaald).</summary>
        public float Load(int pipe) => _load[pipe];

        public void Solve()
        {
            var count = _supply.Count;
            var parent = new int[count];
            var parentPipe = new int[count];
            var order = BuildTree(parent, parentPipe);

            // Vraag per deelboom, van de bladeren naar de wortels.
            var flow = new float[count];
            foreach (var node in order.AsEnumerable().Reverse())
            {
                flow[node] += _demand[node];
                if (parent[node] >= 0)
                {
                    flow[parent[node]] += flow[node];
                }
            }

            // Van de wortels naar de bladeren: elke buis en elke bron kan het deel erachter hoogstens verkleinen.
            _satisfaction = new float[count];
            _load = new float[_pipes.Count];
            foreach (var node in order)
            {
                if (parent[node] < 0)
                {
                    _satisfaction[node] = flow[node] <= 0f ? 1f : Math.Min(1f, _supply[node] / flow[node]);
                    continue;
                }

                var capacity = _pipes[parentPipe[node]].Capacity;
                _load[parentPipe[node]] = flow[node] / capacity;
                var pipeFactor = flow[node] <= 0f ? 1f : Math.Min(1f, capacity / flow[node]);
                _satisfaction[node] = _satisfaction[parent[node]] * pipeFactor;
            }
        }

        private static int Other((int A, int B, float Capacity) pipe, int node)
        {
            if (pipe.A == node)
            {
                return pipe.B;
            }

            return pipe.B == node ? pipe.A : -1;
        }

        /// <summary>Breedte-eerst vanaf alle bronnen; geeft de knopen in volgorde van afstand. Onbereikbare knopen blijven buiten de lijst.</summary>
        private List<int> BuildTree(int[] parent, int[] parentPipe)
        {
            var seen = new bool[_supply.Count];
            var order = new List<int>();
            for (var i = 0; i < _supply.Count; i++)
            {
                parent[i] = -1;
                if (_supply[i] > 0f)
                {
                    seen[i] = true;
                    order.Add(i);
                }
            }

            for (var head = 0; head < order.Count; head++)
            {
                var node = order[head];
                for (var p = 0; p < _pipes.Count; p++)
                {
                    var other = Other(_pipes[p], node);
                    if (other >= 0 && !seen[other])
                    {
                        seen[other] = true;
                        parent[other] = node;
                        parentPipe[other] = p;
                        order.Add(other);
                    }
                }
            }

            return order;
        }
    }
}
