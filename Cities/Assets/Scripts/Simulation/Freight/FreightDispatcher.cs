using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Freight
{
    public enum FreightTruckState : byte
    {
        /// <summary>Staat stil bij <see cref="FreightTruck.At"/>.</summary>
        Idle = 0,

        /// <summary>Rijdt leeg naar de plek waar hij moet laden.</summary>
        ToPickup = 1,

        /// <summary>Rijdt beladen naar de plek waar hij moet lossen.</summary>
        Delivering = 2,
    }

    /// <summary>Eén vrachtwagen van het goederenverkeer.</summary>
    public sealed class FreightTruck
    {
        internal FreightTruck(int id, int at)
        {
            Id = id;
            At = at;
        }

        public int Id { get; }

        /// <summary>Het knooppunt waar hij staat of vandaan komt.</summary>
        public int At { get; internal set; }

        public FreightTruckState State { get; internal set; }

        public int Source { get; internal set; }

        public int Destination { get; internal set; }

        /// <summary>Ton die bij het plannen is toegezegd.</summary>
        public float Planned { get; internal set; }

        /// <summary>Ton aan boord, na het laden.</summary>
        public float Load { get; internal set; }

        /// <summary>Seconden tot het einde van het huidige traject.</summary>
        public float Remaining { get; internal set; }

        /// <summary>Seconden die het beladen traject duurt; gaat in zodra de wagen geladen heeft.</summary>
        public float DeliverySeconds { get; internal set; }
    }

    /// <summary>
    /// Verdeelt het werk over de vrachtwagens (E08-11): een winkel met te weinig voorraad krijgt goederen van de dichtstbijzijnde producent met
    /// voorraad, of van een buitenverbinding (import) als geen producent kan leveren. Producenten met een volle vracht sturen die naar een
    /// buitenverbinding (export). De reistijd komt van de aanroeper (bijvoorbeeld de lanegraaf); een negatieve of niet-eindige waarde betekent
    /// dat er geen route is, en dan gaat er geen wagen op pad.
    /// shortcut: er is één soort goederen; de keten van grondstof via industrie naar handel volgt in E11-08.
    /// </summary>
    public sealed class FreightDispatcher
    {
        /// <summary>Een winkel bestelt als zijn voorraad (met toezeggingen) onder deze helft van de opslag zakt.</summary>
        public const float ReorderShare = 0.5f;

        private readonly Func<FreightNode, FreightNode, float> _travelSeconds;
        private readonly List<FreightTruck> _trucks = new List<FreightTruck>();

        public FreightDispatcher(FreightNetwork network, float truckCapacityTonnes, Func<FreightNode, FreightNode, float> travelSeconds)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            _travelSeconds = travelSeconds ?? throw new ArgumentNullException(nameof(travelSeconds));
            if (!(truckCapacityTonnes > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(truckCapacityTonnes), truckCapacityTonnes, "Een vrachtwagen heeft laadvermogen nodig.");
            }

            TruckCapacity = truckCapacityTonnes;
        }

        public float TruckCapacity { get; }

        public FreightNetwork Network { get; }

        public IReadOnlyList<FreightTruck> Trucks => _trucks;

        public float DeliveredTonnes { get; internal set; }

        public float ExportedTonnes { get; internal set; }

        public float ImportedTonnes { get; internal set; }

        public int Trips { get; internal set; }

        /// <summary>Aantal keer dat een bestelling niet kon worden uitgevoerd omdat er geen route was.</summary>
        public int Unreachable { get; internal set; }

        public FreightTruck AddTruck(int homeNode)
        {
            Network.Get(homeNode);
            var truck = new FreightTruck(_trucks.Count, homeNode);
            _trucks.Add(truck);
            return truck;
        }

        /// <summary>Nettohandel sinds de vorige keer (export positief, import negatief) voor <c>Trade.Settle</c>; zet de tellers terug op nul.</summary>
        public float TakeTradeBalance()
        {
            var balance = ExportedTonnes - ImportedTonnes;
            ExportedTonnes = 0f;
            ImportedTonnes = 0f;
            return balance;
        }

        /// <summary>Eén stap: wagens rijden <paramref name="seconds"/> door en nieuw werk wordt verdeeld.</summary>
        public void Step(float seconds)
        {
            if (!(seconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "De tijdstap kan niet negatief zijn.");
            }

            foreach (var truck in _trucks)
            {
                Advance(truck, seconds);
            }

            PlanShopDeliveries();
            PlanExports();
        }

        internal void Restore(IEnumerable<FreightTruck> trucks)
        {
            _trucks.Clear();
            _trucks.AddRange(trucks);
        }

        private static bool IsRoute(float seconds) => seconds >= 0f && !float.IsInfinity(seconds) && !float.IsNaN(seconds);

        private static void Reserve(FreightNode source, FreightNode destination, float load)
        {
            if (source.Kind == FreightNodeKind.Producer)
            {
                source.Reserved += load;
            }

            if (destination.Kind == FreightNodeKind.Shop)
            {
                destination.Reserved += load;
            }
        }

        private void Advance(FreightTruck truck, float seconds)
        {
            var left = seconds;
            while (truck.State != FreightTruckState.Idle)
            {
                if (truck.Remaining > left)
                {
                    truck.Remaining -= left;
                    return;
                }

                left -= truck.Remaining;
                if (truck.State == FreightTruckState.ToPickup)
                {
                    Load(truck);
                }
                else
                {
                    Unload(truck);
                }
            }
        }

        private void Load(FreightTruck truck)
        {
            var source = Network.Get(truck.Source);
            truck.At = truck.Source;
            truck.Load = truck.Planned;
            if (source.Kind == FreightNodeKind.Producer)
            {
                truck.Load = Math.Min(truck.Planned, source.Level);
                source.Level -= truck.Load;
                source.Reserved = Math.Max(0f, source.Reserved - truck.Planned);
            }

            truck.State = FreightTruckState.Delivering;
            truck.Remaining = truck.DeliverySeconds;
        }

        private void Unload(FreightTruck truck)
        {
            var destination = Network.Get(truck.Destination);
            if (destination.Kind == FreightNodeKind.Shop)
            {
                destination.Level = Math.Min(destination.Capacity, destination.Level + truck.Load);
                destination.Reserved = Math.Max(0f, destination.Reserved - truck.Planned);
                DeliveredTonnes += truck.Load;
                if (Network.Get(truck.Source).Kind == FreightNodeKind.Outside)
                {
                    ImportedTonnes += truck.Load;
                }
            }
            else
            {
                ExportedTonnes += truck.Load;
            }

            Trips++;
            truck.At = truck.Destination;
            truck.State = FreightTruckState.Idle;
            truck.Planned = 0f;
            truck.Load = 0f;
            truck.Remaining = 0f;
            truck.DeliverySeconds = 0f;
        }

        private void PlanShopDeliveries()
        {
            foreach (var shop in Network.Nodes)
            {
                if (shop.Kind == FreightNodeKind.Shop && shop.Level + shop.Reserved < shop.Capacity * ReorderShare)
                {
                    FillShop(shop);
                }
            }
        }

        /// <summary>Bestelt vrachten tot de voorraad met toezeggingen vol is, of tot er geen wagen of route meer is.</summary>
        private void FillShop(FreightNode shop)
        {
            while (shop.Room > 0f)
            {
                var load = Math.Min(TruckCapacity, shop.Room);
                var source = Network.FindNearest(FreightNodeKind.Producer, shop.Position, n => n.AvailableToCollect >= load)
                    ?? Network.FindNearest(FreightNodeKind.Outside, shop.Position, n => true);
                if (source == null || !Dispatch(source, shop, load))
                {
                    return;
                }
            }
        }

        private void PlanExports()
        {
            foreach (var producer in Network.Nodes)
            {
                if (producer.Kind != FreightNodeKind.Producer || producer.AvailableToCollect < TruckCapacity)
                {
                    continue;
                }

                var outside = Network.FindNearest(FreightNodeKind.Outside, producer.Position, n => true);
                if (outside != null)
                {
                    Dispatch(producer, outside, TruckCapacity);
                }
            }
        }

        private bool Dispatch(FreightNode source, FreightNode destination, float load)
        {
            var truck = NearestIdleTruck(source);
            if (truck == null)
            {
                return false;
            }

            var pickup = _travelSeconds(Network.Get(truck.At), source);
            var delivery = _travelSeconds(source, destination);
            if (!IsRoute(pickup) || !IsRoute(delivery))
            {
                Unreachable++;
                return false;
            }

            truck.State = FreightTruckState.ToPickup;
            truck.Source = source.Id;
            truck.Destination = destination.Id;
            truck.Planned = load;
            truck.Remaining = pickup;
            truck.DeliverySeconds = delivery;
            Reserve(source, destination, load);
            return true;
        }

        private FreightTruck NearestIdleTruck(FreightNode source)
        {
            FreightTruck best = null;
            var bestDistance = float.MaxValue;
            foreach (var truck in _trucks)
            {
                if (truck.State != FreightTruckState.Idle)
                {
                    continue;
                }

                var distance = math.distance(Network.Get(truck.At).Position, source.Position);
                if (distance < bestDistance)
                {
                    best = truck;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
