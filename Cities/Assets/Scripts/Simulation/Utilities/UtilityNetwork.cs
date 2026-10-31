using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Het netwerkmodel voor nutsvoorzieningen (E06-01), bruikbaar voor stroom, water en riool. Een knoop levert (capaciteit), verbruikt
    /// (vraag) of allebei. Knopen horen bij hetzelfde net als ze met een leiding zijn verbonden of dicht genoeg bij elkaar staan
    /// (<see cref="ConnectRadius"/>). Elke <see cref="Tick"/> verdeelt het aanbod van een net over de vragers van dat net:
    /// bij een tekort krijgt iedereen in dat net hetzelfde deel van zijn vraag.
    /// </summary>
    public sealed class UtilityNetwork
    {
        private struct Node
        {
            public float2 Position;
            public float Capacity;
            public float Demand;
            public bool Alive;
            public int Network;
            public float Satisfaction;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<HashSet<int>> _links = new List<HashSet<int>>();
        private readonly List<float> _supply = new List<float>();
        private readonly List<float> _demand = new List<float>();
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private int[] _parent = Array.Empty<int>();

        /// <param name="connectRadius">Afstand in meters waarbinnen knopen elkaar zonder leiding aansluiten; 0 zet nabijheid uit.</param>
        public UtilityNetwork(float connectRadius)
        {
            if (connectRadius < 0f || float.IsNaN(connectRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(connectRadius), connectRadius, "De aansluitafstand kan niet negatief zijn.");
            }

            ConnectRadius = connectRadius;
        }

        public float ConnectRadius { get; }

        /// <summary>Het aantal losse netten na de laatste <see cref="Tick"/>.</summary>
        public int NetworkCount => _supply.Count;

        // shortcut: knoop-ID's worden niet hergebruikt en een verwijderde knoop blijft als lege plek staan; bij veel wisselende knopen compact maken.
        public int Add(float2 position, float capacity, float demand)
        {
            CheckAmounts(capacity, demand);
            _nodes.Add(new Node { Position = position, Capacity = capacity, Demand = demand, Alive = true, Network = -1, Satisfaction = 1f });
            _links.Add(new HashSet<int>());
            return _nodes.Count - 1;
        }

        public void Remove(int id)
        {
            Check(id);
            foreach (var other in _links[id])
            {
                _links[other].Remove(id);
            }

            _links[id].Clear();
            var node = _nodes[id];
            node.Alive = false;
            node.Network = -1;
            _nodes[id] = node;
        }

        /// <summary>Wijzigt wat een knoop levert en verbruikt, bijvoorbeeld als een gebouw bezet raakt of een centrale uitvalt.</summary>
        public void Set(int id, float capacity, float demand)
        {
            Check(id);
            CheckAmounts(capacity, demand);
            var node = _nodes[id];
            node.Capacity = capacity;
            node.Demand = demand;
            _nodes[id] = node;
        }

        /// <summary>Verbindt twee knopen met een leiding; geeft false als ze al verbonden waren.</summary>
        public bool Link(int a, int b)
        {
            Check(a);
            Check(b);
            if (a == b)
            {
                throw new ArgumentException("Een knoop kan niet met zichzelf verbonden worden.");
            }

            return _links[a].Add(b) & _links[b].Add(a);
        }

        public bool Unlink(int a, int b)
        {
            Check(a);
            Check(b);
            return _links[a].Remove(b) & _links[b].Remove(a);
        }

        /// <summary>Het net waar de knoop bij hoort; -1 voor de eerste <see cref="Tick"/>.</summary>
        public int NetworkOf(int id)
        {
            Check(id);
            return _nodes[id].Network;
        }

        /// <summary>Welk deel van zijn vraag de knoop krijgt, van 0 tot 1; zonder vraag altijd 1.</summary>
        public float Satisfaction(int id)
        {
            Check(id);
            return _nodes[id].Satisfaction;
        }

        /// <summary>Wat de knoop deze tick krijgt: zijn vraag maal <see cref="Satisfaction"/>.</summary>
        public float Supplied(int id)
        {
            Check(id);
            return _nodes[id].Demand * _nodes[id].Satisfaction;
        }

        public float Supply(int network) => _supply[network];

        public float Demand(int network) => _demand[network];

        /// <summary>Bepaalt de netten opnieuw en verdeelt het aanbod over de vragers.</summary>
        public void Tick()
        {
            Connect();
            var count = Number();
            _supply.Clear();
            _demand.Clear();
            for (var i = 0; i < count; i++)
            {
                _supply.Add(0f);
                _demand.Add(0f);
            }

            foreach (var node in _nodes)
            {
                if (node.Alive)
                {
                    _supply[node.Network] += node.Capacity;
                    _demand[node.Network] += node.Demand;
                }
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Alive)
                {
                    var demand = _demand[node.Network];
                    node.Satisfaction = demand <= 0f ? 1f : math.min(1f, _supply[node.Network] / demand);
                    _nodes[i] = node;
                }
            }
        }

        private static void CheckAmounts(float capacity, float demand)
        {
            if (!(capacity >= 0f) || !(demand >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capaciteit en vraag moeten nul of meer zijn.");
            }
        }

        private void Check(int id)
        {
            if (id < 0 || id >= _nodes.Count || !_nodes[id].Alive)
            {
                throw new ArgumentException($"Onbekende knoop {id}.", nameof(id));
            }
        }

        /// <summary>Union-find over de leidingen en, binnen de aansluitafstand, over naburige knopen.</summary>
        private void Connect()
        {
            if (_parent.Length < _nodes.Count)
            {
                _parent = new int[_nodes.Count];
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                _parent[i] = i;
            }

            for (var i = 0; i < _nodes.Count; i++)
            {
                foreach (var other in _links[i])
                {
                    Union(i, other);
                }
            }

            if (ConnectRadius > 0f)
            {
                ConnectNearby();
            }
        }

        private void ConnectNearby()
        {
            _grid.Clear();
            var radiusSq = ConnectRadius * ConnectRadius;
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (!_nodes[i].Alive)
                {
                    continue;
                }

                var cell = CellOf(_nodes[i].Position);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (!_grid.TryGetValue(Key(cell.x + dx, cell.y + dz), out var near))
                        {
                            continue;
                        }

                        foreach (var other in near)
                        {
                            if (math.distancesq(_nodes[i].Position, _nodes[other].Position) <= radiusSq)
                            {
                                Union(i, other);
                            }
                        }
                    }
                }

                var key = Key(cell.x, cell.y);
                if (!_grid.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    _grid[key] = list;
                }

                list.Add(i);
            }
        }

        /// <summary>Geeft elke wortel van een levende knoop een volgnummer en schrijft dat in de knopen.</summary>
        private int Number()
        {
            var numbers = new Dictionary<int, int>();
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!node.Alive)
                {
                    continue;
                }

                var root = Find(i);
                if (!numbers.TryGetValue(root, out var number))
                {
                    number = numbers.Count;
                    numbers[root] = number;
                }

                node.Network = number;
                _nodes[i] = node;
            }

            return numbers.Count;
        }

        private int2 CellOf(float2 position) => new int2((int)math.floor(position.x / ConnectRadius), (int)math.floor(position.y / ConnectRadius));

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        private int Find(int i)
        {
            while (_parent[i] != i)
            {
                _parent[i] = _parent[_parent[i]];
                i = _parent[i];
            }

            return i;
        }

        private void Union(int a, int b) => _parent[Find(a)] = Find(b);
    }
}
