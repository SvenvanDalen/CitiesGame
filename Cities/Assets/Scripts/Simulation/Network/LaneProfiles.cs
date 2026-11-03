using System;
using System.Collections.Generic;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Bouwt symmetrische dwarsprofielen op: van de hartlijn naar buiten de rijstroken, dan eventueel een fietspad en
    /// een stoep aan beide kanten. De wegtypes van E04-05 gebruiken dit als basis.
    /// </summary>
    public static class LaneProfiles
    {
        public const float CarLaneWidth = 3.25f;
        public const float BikeLaneWidth = 1.8f;
        public const float SidewalkWidth = 2f;
        public const float ParkingLaneWidth = 2.2f;

        /// <param name="carLanes">Rijstroken per richting, of bij <paramref name="oneWay"/> in totaal; 0 voor een voet- of fietspad.</param>
        /// <param name="oneWay">Alle rijstroken heen, symmetrisch rond de hartlijn.</param>
        /// <param name="parking">Een parkeerstrook aan beide kanten, direct naast de rijstroken.</param>
        /// <param name="busLanes">De buitenste rijstrook per richting is een busbaan (E09-11); vraagt minstens twee rijstroken per richting; bij een eenrichtingsweg met één rijstrook is de hele weg een busstraat.</param>
        public static List<LaneSpec> Road(int carLanes, bool bikeLanes, bool sidewalks, bool oneWay = false, bool parking = false, bool busLanes = false)
        {
            if (busLanes && carLanes < (oneWay ? 1 : 2))
            {
                throw new ArgumentException("Een busbaan naast een rijstrook voor auto's vraagt minstens twee rijstroken per richting.", nameof(busLanes));
            }

            if (carLanes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(carLanes), carLanes, "Kan niet negatief zijn.");
            }

            var lanes = new List<LaneSpec>();
            var edge = oneWay ? -carLanes * CarLaneWidth * 0.5f : 0f;
            if (oneWay)
            {
                for (var i = 0; i < carLanes; i++)
                {
                    var kind = busLanes && i == carLanes - 1 ? LaneTypes.Bus : LaneTypes.Vehicles;
                    lanes.Add(new LaneSpec(kind, LaneDirection.Forward, edge + ((i + 0.5f) * CarLaneWidth), CarLaneWidth));
                }

                edge = -edge;
            }
            else
            {
                for (var i = 0; i < carLanes; i++)
                {
                    var offset = (i + 0.5f) * CarLaneWidth;
                    var kind = busLanes && i == carLanes - 1 ? LaneTypes.Bus : LaneTypes.Vehicles;
                    lanes.Add(new LaneSpec(kind, LaneDirection.Forward, offset, CarLaneWidth));
                    lanes.Add(new LaneSpec(kind, LaneDirection.Backward, -offset, CarLaneWidth));
                }

                edge = carLanes * CarLaneWidth;
            }

            if (parking && carLanes > 0)
            {
                lanes.Add(new LaneSpec(LaneTypes.Parking, LaneDirection.Both, edge + (ParkingLaneWidth * 0.5f), ParkingLaneWidth));
                lanes.Add(new LaneSpec(LaneTypes.Parking, LaneDirection.Both, -edge - (ParkingLaneWidth * 0.5f), ParkingLaneWidth));
                edge += ParkingLaneWidth;
            }

            if (bikeLanes)
            {
                lanes.Add(new LaneSpec(LaneTypes.Bike, LaneDirection.Forward, edge + (BikeLaneWidth * 0.5f), BikeLaneWidth));
                lanes.Add(new LaneSpec(LaneTypes.Bike, oneWay ? LaneDirection.Forward : LaneDirection.Backward, -edge - (BikeLaneWidth * 0.5f), BikeLaneWidth));
                edge += BikeLaneWidth;
            }

            if (sidewalks || lanes.Count == 0)
            {
                lanes.Add(new LaneSpec(LaneTypes.Pedestrian, LaneDirection.Both, edge + (SidewalkWidth * 0.5f), SidewalkWidth));
                lanes.Add(new LaneSpec(LaneTypes.Pedestrian, LaneDirection.Both, -edge - (SidewalkWidth * 0.5f), SidewalkWidth));
            }

            return lanes;
        }
    }
}
