using System.Collections.Generic;

namespace Cities.Simulation.Economy
{
    /// <summary>Soort verbinding met de buitenwereld.</summary>
    public enum ConnectionKind
    {
        Road = 0,
        Rail = 1,
        Sea = 2,
        Air = 3,
    }

    /// <summary>Een verbinding met de buitenwereld (snelwegaansluiting, spoor, haven of luchthaven).</summary>
    public readonly struct TradeConnection
    {
        public TradeConnection(int id, ConnectionKind kind, bool isOpen)
        {
            Id = id;
            Kind = kind;
            IsOpen = isOpen;
        }

        public int Id { get; }

        public ConnectionKind Kind { get; }

        public bool IsOpen { get; }
    }

    /// <summary>
    /// Kleine abstractie voor de buitenverbindingen (E14-04/E14-05): de handelsvoorkeuren hoeven alleen te weten welke verbindingen er zijn
    /// en of ze open zijn. Wie de verbindingen bouwt, implementeert dit.
    /// </summary>
    public interface ITradeConnections
    {
        IReadOnlyList<TradeConnection> Connections { get; }
    }

    /// <summary>Eenvoudige vaste lijst verbindingen, voor tests en tot de echte verbindingen er zijn.</summary>
    public sealed class FixedTradeConnections : ITradeConnections
    {
        private readonly List<TradeConnection> _connections = new List<TradeConnection>();

        public IReadOnlyList<TradeConnection> Connections => _connections;

        public void Set(int id, ConnectionKind kind, bool isOpen)
        {
            _connections.RemoveAll(c => c.Id == id);
            _connections.Add(new TradeConnection(id, kind, isOpen));
        }

        public void Remove(int id) => _connections.RemoveAll(c => c.Id == id);
    }
}
