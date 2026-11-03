using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Een stuk spoor tussen twee knopen van het <see cref="RailNetwork"/> (E09-07). Onveranderlijk.</summary>
    public sealed class RailTrack
    {
        public RailTrack(int id, int a, int b, float length)
        {
            if (a == b)
            {
                throw new ArgumentException("Een spoor verbindt twee verschillende knopen.", nameof(b));
            }

            if (!(length >= Train.MinTrackLength))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, $"Een spoor is minstens {Train.MinTrackLength} m lang.");
            }

            Id = id;
            A = a;
            B = b;
            Length = length;
        }

        public int Id { get; }

        public int A { get; }

        public int B { get; }

        public float Length { get; }

        public bool Touches(int node) => A == node || B == node;

        public int Other(int node) => node == A ? B : A;
    }

    /// <summary>Een station op een knoop: een perron voor reizigers of een goederenstation (E09-07). Onveranderlijk.</summary>
    public sealed class RailStation
    {
        public RailStation(int node, TrainKind kind, int stop, float platformLength)
        {
            Train.CheckPlatform(platformLength);
            if (kind == TrainKind.Passenger && stop < 0)
            {
                throw new ArgumentException("Een passagiersstation hoort bij een halte.", nameof(stop));
            }

            Node = node;
            Kind = kind;
            Stop = kind == TrainKind.Passenger ? stop : -1;
            PlatformLength = platformLength;
        }

        public int Node { get; }

        public TrainKind Kind { get; }

        /// <summary>De halte in het <see cref="TransitNetwork"/>; -1 voor een goederenstation.</summary>
        public int Stop { get; }

        public float PlatformLength { get; }
    }

    /// <summary>Een overweg (E09-07): een kruising van een spoor met een weg, met bomen die dicht gaan als een trein nadert.</summary>
    public sealed class LevelCrossing
    {
        public LevelCrossing(int id, int track, float offset, int road)
        {
            Id = id;
            Track = track;
            Offset = offset;
            Road = road;
        }

        public int Id { get; }

        public int Track { get; }

        /// <summary>Afstand langs het spoor vanaf knoop <see cref="RailTrack.A"/> in meter.</summary>
        public float Offset { get; }

        /// <summary>De weg die het spoor kruist (segment-ID van het wegennet).</summary>
        public int Road { get; }

        /// <summary>Of de bomen dicht zijn. Wordt afgeleid van de treinen en dus niet bewaard.</summary>
        public bool IsClosed { get; internal set; }
    }

    /// <summary>Een rijdende trein (E09-07): een route van knopen en de afstand van de neus langs die route.</summary>
    public sealed class TrainRun
    {
        private readonly int[] _route;

        public TrainRun(int id, TrainKind kind, int cars, IReadOnlyList<int> route, float position)
        {
            if (route == null || route.Count < 2)
            {
                throw new ArgumentException("Een route heeft minstens twee knopen.", nameof(route));
            }

            Length = Train.Length(kind, cars);
            Id = id;
            Kind = kind;
            Cars = cars;
            _route = new int[route.Count];
            for (var i = 0; i < _route.Length; i++)
            {
                _route[i] = route[i];
            }

            Position = position;
        }

        public int Id { get; }

        public TrainKind Kind { get; }

        public int Cars { get; }

        /// <summary>De hele treinlengte in meter.</summary>
        public float Length { get; }

        public IReadOnlyList<int> Route => _route;

        /// <summary>Afstand van de neus langs de route in meter; kleiner dan nul als de trein nog niet is gestart.</summary>
        public float Position { get; internal set; }

        /// <summary>Keert de trein om op het eindstation: dezelfde route terug, de neus weer aan het begin.</summary>
        internal void Reverse()
        {
            Array.Reverse(_route);
            Position = 0f;
        }
    }

    /// <summary>De complete toestand van een <see cref="RailNetwork"/>, voor opslaan en laden (E09-07).</summary>
    public sealed class RailSnapshot
    {
        public IReadOnlyList<KeyValuePair<int, float2>> Nodes { get; set; } = Array.Empty<KeyValuePair<int, float2>>();

        public IReadOnlyList<RailTrack> Tracks { get; set; } = Array.Empty<RailTrack>();

        public IReadOnlyList<RailStation> Stations { get; set; } = Array.Empty<RailStation>();

        /// <summary>Per wissel (knoop) het gekozen spoor.</summary>
        public IReadOnlyList<KeyValuePair<int, int>> Switches { get; set; } = Array.Empty<KeyValuePair<int, int>>();

        public IReadOnlyList<LevelCrossing> Crossings { get; set; } = Array.Empty<LevelCrossing>();

        public IReadOnlyList<TrainRun> Trains { get; set; } = Array.Empty<TrainRun>();

        public int NextNode { get; set; } = 1;

        public int NextTrack { get; set; } = 1;

        public int NextCrossing { get; set; } = 1;

        public int NextTrain { get; set; } = 1;
    }
}
