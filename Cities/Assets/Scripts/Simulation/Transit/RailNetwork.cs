using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// Het spoornet van de stad (E09-07): knopen en spoor ertussen, stations (passagiers en goederen), wissels waar drie sporen samenkomen,
    /// overwegen waar het spoor een weg kruist en de rijdende treinen. Een passagiersstation is ook een halte in het <see cref="TransitNetwork"/>;
    /// een passagierslijn is een gewone <see cref="TransitLine"/> waarvan alle haltes stations zijn die over het spoor te bereiken zijn.
    /// Gebruik de methodes van deze klasse in plaats van die van het <see cref="TransitNetwork"/>, zodat de regels van <see cref="Train"/> gelden.
    /// </summary>
    /// <remarks>
    /// Een wissel heeft een stam (het spoor met het laagste ID) en twee of meer takken, waarvan er één is gekozen. Van de stam naar de tak
    /// gaat de trein over het gekozen spoor; van een tak naar de stam mag alleen als die tak gekozen is (een verkeerd gelegde wissel blokkeert),
    /// en van tak naar tak kan niet. shortcut: er is nog geen seinstelsel; een route legt bij het starten de wissels goed, maar twee treinen
    /// op één spoor botsen niet.
    /// </remarks>
    public sealed class RailNetwork
    {
        private const int MaxTracksPerNode = 3;
        private const int MaxTracksPerStation = 2;

        private readonly TransitNetwork _transit;
        private readonly SortedDictionary<int, float2> _nodes = new SortedDictionary<int, float2>();
        private readonly SortedDictionary<int, RailTrack> _tracks = new SortedDictionary<int, RailTrack>();
        private readonly SortedDictionary<int, RailStation> _stations = new SortedDictionary<int, RailStation>();
        private readonly SortedDictionary<int, int> _switches = new SortedDictionary<int, int>();
        private readonly SortedDictionary<int, LevelCrossing> _crossings = new SortedDictionary<int, LevelCrossing>();
        private readonly SortedDictionary<int, TrainRun> _trains = new SortedDictionary<int, TrainRun>();
        private int _nextNode = 1;
        private int _nextTrack = 1;
        private int _nextCrossing = 1;
        private int _nextTrain = 1;

        public RailNetwork(TransitNetwork transit)
        {
            _transit = transit ?? throw new ArgumentNullException(nameof(transit));
        }

        public TransitNetwork Transit => _transit;

        public IEnumerable<KeyValuePair<int, float2>> Nodes => _nodes;

        public IEnumerable<RailTrack> Tracks => _tracks.Values;

        public IEnumerable<RailStation> Stations => _stations.Values;

        /// <summary>De wissels: knoop en het gekozen spoor.</summary>
        public IEnumerable<KeyValuePair<int, int>> Switches => _switches;

        public IEnumerable<LevelCrossing> Crossings => _crossings.Values;

        public IEnumerable<TrainRun> Trains => _trains.Values;

        public int NextNodeId => _nextNode;

        public int NextTrackId => _nextTrack;

        public int NextCrossingId => _nextCrossing;

        public int NextTrainId => _nextTrain;

        public bool HasNode(int node) => _nodes.ContainsKey(node);

        public float2 PositionOf(int node) =>
            _nodes.TryGetValue(node, out var position) ? position : throw new ArgumentException($"Knoop {node} bestaat niet.", nameof(node));

        public RailTrack GetTrack(int id) =>
            _tracks.TryGetValue(id, out var track) ? track : throw new ArgumentException($"Spoor {id} bestaat niet.", nameof(id));

        public TrainRun GetTrain(int id) =>
            _trains.TryGetValue(id, out var train) ? train : throw new ArgumentException($"Trein {id} bestaat niet.", nameof(id));

        public LevelCrossing GetCrossing(int id) =>
            _crossings.TryGetValue(id, out var crossing) ? crossing : throw new ArgumentException($"Overweg {id} bestaat niet.", nameof(id));

        public bool IsStation(int node) => _stations.ContainsKey(node);

        public RailStation GetStation(int node) =>
            _stations.TryGetValue(node, out var station) ? station : throw new ArgumentException($"Knoop {node} is geen station.", nameof(node));

        /// <summary>De knoop van het station bij een halte, of -1 als de halte geen treinstation is.</summary>
        public int NodeOfStop(int stop) => _stations.Values.Where(s => s.Stop == stop).Select(s => s.Node).DefaultIfEmpty(-1).First();

        public int AddNode(float2 position)
        {
            var id = _nextNode++;
            _nodes.Add(id, position);
            return id;
        }

        /// <summary>Verwijdert een knoop zonder spoor en zonder station.</summary>
        public bool TryRemoveNode(int node) => !IsStation(node) && Degree(node) == 0 && _nodes.Remove(node);

        /// <summary>De sporen die aan een knoop liggen, op volgorde van ID.</summary>
        public IReadOnlyList<RailTrack> TracksAt(int node) => _tracks.Values.Where(t => t.Touches(node)).ToList();

        public int Degree(int node) => _tracks.Values.Count(t => t.Touches(node));

        /// <summary>Legt spoor tussen twee knopen. Een knoop heeft hoogstens drie sporen (dan is het een wissel), een station hoogstens twee.</summary>
        public int AddTrack(int a, int b)
        {
            var length = math.distance(PositionOf(a), PositionOf(b));
            var track = new RailTrack(_nextTrack, a, b, length);
            if (TrackBetween(a, b) != null)
            {
                throw new ArgumentException($"Tussen knoop {a} en {b} ligt al spoor.", nameof(b));
            }

            CheckRoomFor(a);
            CheckRoomFor(b);
            _nextTrack++;
            _tracks.Add(track.Id, track);
            EnsureSwitch(a);
            EnsureSwitch(b);
            return track.Id;
        }

        /// <summary>Haalt spoor weg; geeft false als er een overweg op ligt of een trein het gebruikt.</summary>
        public bool TryRemoveTrack(int id)
        {
            if (!_tracks.TryGetValue(id, out var track) || _crossings.Values.Any(c => c.Track == id) || _trains.Values.Any(t => Spans(t).Any(s => s.Track == id)))
            {
                return false;
            }

            _tracks.Remove(id);
            DropSwitchIfPlain(track.A);
            DropSwitchIfPlain(track.B);
            return true;
        }

        public TrainRun[] TrainsOn(int track) => _trains.Values.Where(t => Spans(t).Any(s => s.Track == track && Overlaps(t, s))).ToArray();

        public bool IsOccupied(int track) => _trains.Values.Any(t => Spans(t).Any(s => s.Track == track && Overlaps(t, s)));

        public bool IsSwitch(int node) => _switches.ContainsKey(node);

        /// <summary>De stam van een wissel: het spoor met het laagste ID.</summary>
        public int StemOf(int node) =>
            IsSwitch(node) ? TracksAt(node)[0].Id : throw new ArgumentException($"Knoop {node} is geen wissel.", nameof(node));

        /// <summary>Het spoor waar de wissel nu naartoe staat (een tak).</summary>
        public int SelectedTrack(int node) =>
            _switches.TryGetValue(node, out var track) ? track : throw new ArgumentException($"Knoop {node} is geen wissel.", nameof(node));

        /// <summary>
        /// Legt een wissel om naar een tak. Geeft false als er een trein op een spoor van de wissel staat; gooit een
        /// <see cref="ArgumentException"/> als het geen wissel is of het spoor geen tak van de wissel is.
        /// </summary>
        public bool TrySetSwitch(int node, int track)
        {
            if (StemOf(node) == track || !GetTrack(track).Touches(node))
            {
                throw new ArgumentException($"Spoor {track} is geen tak van wissel {node}.", nameof(track));
            }

            if (_switches[node] == track)
            {
                return true;
            }

            if (TracksAt(node).Any(t => IsOccupied(t.Id)))
            {
                return false;
            }

            _switches[node] = track;
            return true;
        }

        /// <summary>Het spoor dat een trein neemt na knoop <paramref name="node"/> als hij over <paramref name="fromTrack"/> aankomt; -1 aan het eind of als de wissel verkeerd ligt.</summary>
        public int NextTrack(int node, int fromTrack)
        {
            var tracks = TracksAt(node);
            if (!IsSwitch(node))
            {
                return tracks.Where(t => t.Id != fromTrack).Select(t => t.Id).DefaultIfEmpty(-1).First();
            }

            var selected = _switches[node];
            if (fromTrack == tracks[0].Id)
            {
                return selected;
            }

            return fromTrack == selected ? tracks[0].Id : -1;
        }

        /// <summary>Zet een station op een knoop (geen wissel). Een passagiersstation wordt ook een halte van het openbaar vervoer.</summary>
        public void AddStation(int node, TrainKind kind, float platformLength)
        {
            var position = PositionOf(node);
            Train.CheckPlatform(platformLength);
            if (IsStation(node) || IsSwitch(node) || Degree(node) > MaxTracksPerStation)
            {
                throw new ArgumentException($"Op knoop {node} kan geen station komen.", nameof(node));
            }

            var stop = -1;
            if (kind == TrainKind.Passenger)
            {
                CheckSpacing(position);
                stop = _transit.AddStop(position);
            }

            _stations.Add(node, new RailStation(node, kind, stop, platformLength));
        }

        /// <summary>Haalt een station weg; geeft false als het niet bestaat, een trein er langs rijdt of een lijn de halte gebruikt.</summary>
        public bool TryRemoveStation(int node)
        {
            if (!_stations.TryGetValue(node, out var station) || _trains.Values.Any(t => t.Route.Contains(node)))
            {
                return false;
            }

            if (station.Stop >= 0 && !_transit.TryRemoveStop(station.Stop))
            {
                return false;
            }

            _stations.Remove(node);
            return true;
        }

        /// <summary>Controleert of de haltes een geldige treinlijn vormen; gooit een <see cref="ArgumentException"/> met de reden.</summary>
        public void CheckLine(IReadOnlyList<int> stops, bool isLoop)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            var nodes = stops.Select(NodeOfStop).ToArray();
            for (var i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] < 0)
                {
                    throw new ArgumentException($"Halte {stops[i]} is geen treinstation.", nameof(stops));
                }
            }

            for (var i = 1; i < nodes.Length; i++)
            {
                CheckConnected(nodes[i - 1], nodes[i]);
            }

            if (isLoop && nodes.Length > 1)
            {
                CheckConnected(nodes[nodes.Length - 1], nodes[0]);
            }
        }

        public int AddLine(string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            CheckLine(stops, isLoop);
            return _transit.AddLine(name, stops, isLoop, vehicles, headway);
        }

        /// <summary>
        /// De kortste route over het spoor tussen twee knopen, als rij knopen; leeg als er geen route is. Houdt rekening met de wissels:
        /// van tak naar tak kan niet.
        /// </summary>
        public IReadOnlyList<int> FindPath(int from, int to)
        {
            PositionOf(from);
            PositionOf(to);
            if (from == to)
            {
                return new[] { from };
            }

            var dist = new Dictionary<(int Node, int Via), float>();
            var prev = new Dictionary<(int Node, int Via), (int Node, int Via)>();
            var open = new List<(int Node, int Via)>();
            var start = (from, 0);
            dist[start] = 0f;
            open.Add(start);
            while (open.Count > 0)
            {
                var cur = TakeNearest(open, dist);
                if (cur.Node == to)
                {
                    return Rebuild(prev, cur);
                }

                Relax(cur, dist, prev, open);
            }

            return Array.Empty<int>();
        }

        /// <summary>Zet een trein op een route tussen twee stations van dezelfde soort. De wissels worden voor de route goed gelegd.</summary>
        public int AddTrain(TrainKind kind, int fromNode, int toNode, int cars)
        {
            var from = GetStation(fromNode);
            var to = GetStation(toNode);
            if (from.Kind != kind || to.Kind != kind)
            {
                throw new ArgumentException("Begin- en eindstation moeten bij de soort trein passen.");
            }

            var maxCars = Train.MaxCarsFor(kind, math.min(from.PlatformLength, to.PlatformLength));
            if (cars < 1 || cars > maxCars)
            {
                throw new ArgumentOutOfRangeException(nameof(cars), cars, $"Langs deze perrons passen 1 tot {maxCars} wagons.");
            }

            var path = FindPath(fromNode, toNode);
            if (path.Count < 2)
            {
                throw new InvalidOperationException($"Er is geen route van station {fromNode} naar {toNode}.");
            }

            var train = new TrainRun(_nextTrain, kind, cars, path, 0f);
            if (!AlignSwitches(train.Route))
            {
                throw new InvalidOperationException("Een wissel op de route is bezet door een andere trein.");
            }

            _nextTrain++;
            _trains.Add(train.Id, train);
            return train.Id;
        }

        public bool RemoveTrain(int id)
        {
            var removed = _trains.Remove(id);
            UpdateCrossings();
            return removed;
        }

        /// <summary>Legt de wissels op een route goed; geeft false als een wissel bezet is en niet kon worden omgelegd.</summary>
        public bool AlignSwitches(IReadOnlyList<int> route)
        {
            var aligned = true;
            for (var i = 1; i < route.Count - 1; i++)
            {
                if (IsSwitch(route[i]))
                {
                    aligned &= AlignSwitch(route[i - 1], route[i], route[i + 1]);
                }
            }

            return aligned;
        }

        /// <summary>Zet een overweg op een spoor op <paramref name="offset"/> meter vanaf knoop A, over een weg.</summary>
        public int AddCrossing(int track, float offset, int road)
        {
            var length = GetTrack(track).Length;
            if (!(offset >= Train.CrossingMargin) || !(offset <= length - Train.CrossingMargin))
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, $"Een overweg ligt minstens {Train.CrossingMargin} m van het einde van het spoor.");
            }

            if (_crossings.Values.Any(c => c.Track == track && math.abs(c.Offset - offset) < Train.CrossingSpacing))
            {
                throw new ArgumentException($"Een overweg ligt te dicht bij een andere (minimaal {Train.CrossingSpacing} m).", nameof(offset));
            }

            var crossing = new LevelCrossing(_nextCrossing++, track, offset, road);
            _crossings.Add(crossing.Id, crossing);
            UpdateCrossings();
            return crossing.Id;
        }

        public bool RemoveCrossing(int id) => _crossings.Remove(id);

        /// <summary>De overwegen waar een weg het spoor kruist.</summary>
        public IEnumerable<LevelCrossing> CrossingsOnRoad(int road) => _crossings.Values.Where(c => c.Road == road);

        /// <summary>Laat alle treinen <paramref name="seconds"/> seconden rijden en werkt de overwegen bij. Aan het eind van de route keert de trein om.</summary>
        public void Advance(float seconds)
        {
            if (!(seconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "De tijd kan niet negatief zijn.");
            }

            foreach (var train in _trains.Values)
            {
                train.Position += Train.TopSpeed(train.Kind) * seconds;
                if (train.Position >= RouteLength(train))
                {
                    train.Reverse();
                    AlignSwitches(train.Route);
                }
            }

            UpdateCrossings();
        }

        /// <summary>Werkt de bomen van alle overwegen bij op grond van de treinen die naderen of passeren.</summary>
        public void UpdateCrossings()
        {
            foreach (var crossing in _crossings.Values)
            {
                crossing.IsClosed = _trains.Values.Any(t => ShouldClose(t, crossing));
            }
        }

        public float RouteLength(TrainRun train) => Spans(train).Sum(s => s.End - s.Start);

        /// <summary>De reistijd in seconden tussen twee stations over de kortste route, zonder oponthoud; -1 als er geen route is.</summary>
        public float RideTime(TrainKind kind, int fromNode, int toNode)
        {
            var path = FindPath(fromNode, toNode);
            if (path.Count < 2)
            {
                return -1f;
            }

            var distance = 0f;
            for (var i = 1; i < path.Count; i++)
            {
                distance += TrackBetween(path[i - 1], path[i]).Length;
            }

            return Train.TravelTime(kind, distance);
        }

        /// <summary>Vervangt de hele toestand door een eerder bewaarde.</summary>
        public void Restore(RailSnapshot snapshot)
        {
            Check(snapshot);
            _nodes.Clear();
            _tracks.Clear();
            _stations.Clear();
            _switches.Clear();
            _crossings.Clear();
            _trains.Clear();
            foreach (var node in snapshot.Nodes)
            {
                _nodes.Add(node.Key, node.Value);
            }

            foreach (var track in snapshot.Tracks)
            {
                _tracks.Add(track.Id, track);
            }

            foreach (var station in snapshot.Stations)
            {
                _stations.Add(station.Node, station);
            }

            foreach (var item in snapshot.Switches)
            {
                _switches.Add(item.Key, item.Value);
            }

            foreach (var crossing in snapshot.Crossings)
            {
                _crossings.Add(crossing.Id, crossing);
            }

            foreach (var train in snapshot.Trains)
            {
                _trains.Add(train.Id, train);
            }

            _nextNode = snapshot.NextNode;
            _nextTrack = snapshot.NextTrack;
            _nextCrossing = snapshot.NextCrossing;
            _nextTrain = snapshot.NextTrain;
            UpdateCrossings();
        }

        /// <summary>Controleert een bewaarde toestand op samenhang; gooit een <see cref="ArgumentException"/> met de reden.</summary>
        public static void Check(RailSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var nodeIds = new HashSet<int>(snapshot.Nodes.Select(n => n.Key));
            if (nodeIds.Count != snapshot.Nodes.Count || snapshot.Nodes.Any(n => n.Key >= snapshot.NextNode))
            {
                throw new ArgumentException("Knopen zijn dubbel of hebben een te hoog nummer.");
            }

            CheckTracks(snapshot, nodeIds);
            CheckStationsAndSwitches(snapshot, nodeIds);
            CheckCrossings(snapshot);
            CheckTrains(snapshot, nodeIds);
        }

        private static void CheckTracks(RailSnapshot snapshot, HashSet<int> nodeIds)
        {
            var ids = new HashSet<int>();
            var pairs = new HashSet<(int, int)>();
            foreach (var track in snapshot.Tracks)
            {
                if (!ids.Add(track.Id) || track.Id >= snapshot.NextTrack)
                {
                    throw new ArgumentException($"Spoor {track.Id} is dubbel of heeft een te hoog nummer.");
                }

                if (!nodeIds.Contains(track.A) || !nodeIds.Contains(track.B))
                {
                    throw new ArgumentException($"Spoor {track.Id} verwijst naar een onbekende knoop.");
                }

                if (!pairs.Add((math.min(track.A, track.B), math.max(track.A, track.B))))
                {
                    throw new ArgumentException($"Tussen knoop {track.A} en {track.B} ligt dubbel spoor.");
                }
            }

            if (nodeIds.Any(n => snapshot.Tracks.Count(t => t.Touches(n)) > MaxTracksPerNode))
            {
                throw new ArgumentException("Een knoop heeft te veel sporen.");
            }
        }

        private static void CheckStationsAndSwitches(RailSnapshot snapshot, HashSet<int> nodeIds)
        {
            var switchNodes = new HashSet<int>();
            foreach (var item in snapshot.Switches)
            {
                var tracksAt = snapshot.Tracks.Where(t => t.Touches(item.Key)).OrderBy(t => t.Id).ToArray();
                if (!switchNodes.Add(item.Key) || tracksAt.Length != MaxTracksPerNode || tracksAt[0].Id == item.Value || tracksAt.All(t => t.Id != item.Value))
                {
                    throw new ArgumentException($"Wissel {item.Key} is ongeldig.");
                }
            }

            if (nodeIds.Any(n => !switchNodes.Contains(n) && snapshot.Tracks.Count(t => t.Touches(n)) == MaxTracksPerNode))
            {
                throw new ArgumentException("Een knoop met drie sporen mist een wissel.");
            }

            var duplicate = snapshot.Stations.GroupBy(s => s.Node).Where(g => g.Count() > 1).Select(g => g.Key).DefaultIfEmpty(-1).First();
            var invalid = snapshot.Stations
                .Where(s => !nodeIds.Contains(s.Node) || switchNodes.Contains(s.Node) || snapshot.Tracks.Count(t => t.Touches(s.Node)) > MaxTracksPerStation)
                .Select(s => s.Node)
                .DefaultIfEmpty(duplicate)
                .First();
            if (invalid >= 0)
            {
                throw new ArgumentException($"Station op knoop {invalid} is ongeldig.");
            }
        }

        private static void CheckCrossings(RailSnapshot snapshot)
        {
            var ids = new HashSet<int>();
            foreach (var crossing in snapshot.Crossings)
            {
                var track = snapshot.Tracks.FirstOrDefault(t => t.Id == crossing.Track);
                if (!ids.Add(crossing.Id) || crossing.Id >= snapshot.NextCrossing || track == null
                    || !(crossing.Offset >= Train.CrossingMargin) || !(crossing.Offset <= track.Length - Train.CrossingMargin))
                {
                    throw new ArgumentException($"Overweg {crossing.Id} is ongeldig.");
                }
            }
        }

        private static void CheckTrains(RailSnapshot snapshot, HashSet<int> nodeIds)
        {
            var ids = new HashSet<int>();
            foreach (var train in snapshot.Trains)
            {
                var connected = true;
                for (var i = 1; i < train.Route.Count; i++)
                {
                    var a = train.Route[i - 1];
                    var b = train.Route[i];
                    connected &= snapshot.Tracks.Any(t => t.Touches(a) && t.Touches(b));
                }

                if (!ids.Add(train.Id) || train.Id >= snapshot.NextTrain || !connected || !train.Route.All(nodeIds.Contains)
                    || float.IsNaN(train.Position) || float.IsInfinity(train.Position))
                {
                    throw new ArgumentException($"Trein {train.Id} is ongeldig.");
                }
            }
        }

        private void CheckRoomFor(int node)
        {
            var degree = Degree(node);
            if (degree >= MaxTracksPerNode || (IsStation(node) && degree >= MaxTracksPerStation))
            {
                throw new ArgumentException($"Knoop {node} heeft geen ruimte voor meer spoor.", nameof(node));
            }
        }

        private void EnsureSwitch(int node)
        {
            var tracks = TracksAt(node);
            if (tracks.Count == MaxTracksPerNode && !IsSwitch(node))
            {
                _switches.Add(node, tracks[1].Id);
            }
        }

        private void DropSwitchIfPlain(int node)
        {
            if (IsSwitch(node) && Degree(node) < MaxTracksPerNode)
            {
                _switches.Remove(node);
            }
        }

        private void CheckSpacing(float2 position)
        {
            var tooClose = _stations.Values
                .Where(s => s.Kind == TrainKind.Passenger && math.distance(_nodes[s.Node], position) < Train.MinStationSpacing)
                .Select(s => s.Node)
                .DefaultIfEmpty(-1)
                .First();
            if (tooClose >= 0)
            {
                throw new ArgumentException($"Het station ligt te dicht bij station {tooClose} (minimaal {Train.MinStationSpacing} m).");
            }
        }

        private void CheckConnected(int fromNode, int toNode)
        {
            if (FindPath(fromNode, toNode).Count < 2)
            {
                throw new ArgumentException($"Station {fromNode} en {toNode} zijn niet met elkaar verbonden over het spoor.");
            }
        }

        private bool CanTurn(int node, int via, int next)
        {
            if (via == 0)
            {
                return true;
            }

            if (via == next)
            {
                return false;
            }

            return !IsSwitch(node) || via == StemOf(node) || next == StemOf(node);
        }

        private static (int Node, int Via) TakeNearest(List<(int Node, int Via)> open, Dictionary<(int Node, int Via), float> dist)
        {
            var best = 0;
            for (var i = 1; i < open.Count; i++)
            {
                if (dist[open[i]] < dist[open[best]])
                {
                    best = i;
                }
            }

            var item = open[best];
            open.RemoveAt(best);
            return item;
        }

        private void Relax((int Node, int Via) cur, Dictionary<(int Node, int Via), float> dist, Dictionary<(int Node, int Via), (int Node, int Via)> prev, List<(int Node, int Via)> open)
        {
            foreach (var track in TracksAt(cur.Node))
            {
                if (!CanTurn(cur.Node, cur.Via, track.Id))
                {
                    continue;
                }

                var next = (track.Other(cur.Node), track.Id);
                var candidate = dist[cur] + track.Length;
                var known = dist.TryGetValue(next, out var old);
                if (known && candidate >= old)
                {
                    continue;
                }

                if (!known)
                {
                    open.Add(next);
                }

                dist[next] = candidate;
                prev[next] = cur;
            }
        }

        private static IReadOnlyList<int> Rebuild(Dictionary<(int Node, int Via), (int Node, int Via)> prev, (int Node, int Via) goal)
        {
            var nodes = new List<int> { goal.Node };
            var cur = goal;
            while (prev.TryGetValue(cur, out var before))
            {
                nodes.Add(before.Node);
                cur = before;
            }

            nodes.Reverse();
            return nodes;
        }

        private bool AlignSwitch(int before, int node, int after)
        {
            var entering = TrackBetween(before, node).Id;
            var leaving = TrackBetween(node, after).Id;
            var needed = entering == StemOf(node) ? leaving : entering;
            return TrySetSwitch(node, needed);
        }

        private RailTrack TrackBetween(int a, int b) => _tracks.Values.FirstOrDefault(t => t.Touches(a) && t.Touches(b));

        private IEnumerable<TrackSpan> Spans(TrainRun train)
        {
            var start = 0f;
            for (var i = 1; i < train.Route.Count; i++)
            {
                var track = TrackBetween(train.Route[i - 1], train.Route[i]);
                yield return new TrackSpan(track.Id, start, start + track.Length, track.A == train.Route[i - 1]);
                start += track.Length;
            }
        }

        private static bool Overlaps(TrainRun train, TrackSpan span) => train.Position - train.Length < span.End && train.Position > span.Start;

        private bool ShouldClose(TrainRun train, LevelCrossing crossing)
        {
            foreach (var span in Spans(train))
            {
                if (span.Track == crossing.Track)
                {
                    var along = span.Forward ? crossing.Offset : span.End - span.Start - crossing.Offset;
                    return Train.ShouldClose(span.Start + along - train.Position, Train.TopSpeed(train.Kind), train.Length);
                }
            }

            return false;
        }

        private readonly struct TrackSpan
        {
            public TrackSpan(int track, float start, float end, bool forward)
            {
                Track = track;
                Start = start;
                End = end;
                Forward = forward;
            }

            public int Track { get; }

            public float Start { get; }

            public float End { get; }

            /// <summary>Of de trein het spoor van knoop A naar knoop B berijdt.</summary>
            public bool Forward { get; }
        }
    }
}
