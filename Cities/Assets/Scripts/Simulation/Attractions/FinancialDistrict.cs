using System;
using Unity.Mathematics;

namespace Cities.Simulation.Attractions
{
    /// <summary>
    /// Financiele districten en hotels (E25-10): kantoorcluster, beurs, hotels en conferenties. De beurs geeft een bonus op
    /// kantoorbelasting; hotels vullen met toeristen en conferenties vullen de kamers buiten het seizoen.
    /// </summary>
    public sealed class FinancialDistrict
    {
        public const int RoomsPerHotel = 120;

        public int OfficeBuildings { get; set; }

        public bool HasExchange { get; set; }

        public int Hotels { get; set; }

        /// <summary>Beursbonus op kantoorbelasting: +20 % met beurs en minstens vier kantoren, anders niets.</summary>
        public float OfficeTaxFactor => HasExchange && OfficeBuildings >= 4 ? 1.2f : 1f;

        /// <summary>Bezette kamers uit toeristenvraag en conferentiegasten per dag.</summary>
        public int OccupiedRooms(int tourists, int conferenceGuests)
        {
            if (tourists < 0 || conferenceGuests < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tourists), "Gasten zijn niet negatief.");
            }

            return math.min(Hotels * RoomsPerHotel, tourists + conferenceGuests);
        }

        public long HotelIncomePerDay(int tourists, int conferenceGuests, int pricePerRoom = 90) =>
            (long)OccupiedRooms(tourists, conferenceGuests) * pricePerRoom;
    }
}
