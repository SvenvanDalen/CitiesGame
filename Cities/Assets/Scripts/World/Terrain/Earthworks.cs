using System;

namespace Cities.World.Terrain
{
    /// <summary>Waarom grondverzet niet kan.</summary>
    public enum EarthworksBlock : byte
    {
        None,

        /// <summary>Er is geen depot (bron of put) om grond uit te halen of in te storten.</summary>
        NoDepot,

        /// <summary>Het depot heeft te weinig grond om op te hogen.</summary>
        NotEnoughSoil,

        /// <summary>Het depot heeft te weinig ruimte voor de afgegraven grond.</summary>
        DepotFull,
    }

    /// <summary>De voorraad grond van een bron of put: ophogen haalt er grond uit, afgraven stort er grond in.</summary>
    public sealed class SoilDepot
    {
        public SoilDepot(float capacity, float stock)
        {
            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Het depot heeft een inhoud groter dan nul nodig.");
            }

            Capacity = capacity;
            Stock = Math.Max(0f, Math.Min(stock, capacity));
        }

        /// <summary>Maximale inhoud in kubieke meter.</summary>
        public float Capacity { get; }

        /// <summary>Grond die nu aanwezig is, in kubieke meter.</summary>
        public float Stock { get; private set; }

        public float Space => Capacity - Stock;

        /// <summary>Haalt grond weg (positief) of stort grond in (negatief); de grenzen worden bewaakt door <see cref="Earthworks.Quote"/>.</summary>
        internal void Move(float net) => Stock = Math.Max(0f, Math.Min(Capacity, Stock - net));
    }

    /// <summary>Wat een streek grondverzet kost en of het kan; dit is wat de preview toont.</summary>
    public readonly struct EarthworksQuote
    {
        public EarthworksQuote(float cut, float fill, float cost, EarthworksBlock block)
        {
            Cut = cut;
            Fill = fill;
            Cost = cost;
            Block = block;
        }

        /// <summary>Afgegraven grond in kubieke meter.</summary>
        public float Cut { get; }

        /// <summary>Opgehoogde grond in kubieke meter.</summary>
        public float Fill { get; }

        public float Cost { get; }

        public EarthworksBlock Block { get; }

        public bool Feasible => Block == EarthworksBlock.None;

        /// <summary>Netto grond die het depot uit moet (positief) of in gaat (negatief).</summary>
        public float Net => Fill - Cut;
    }

    /// <summary>
    /// Grondverzet kost geld per kubieke meter die wordt verplaatst en heeft een depot nodig voor het verschil tussen afgraven en
    /// ophogen. De hoeveelheden komen uit de hoogteverschillen van de terreinpenseel (zie <c>EarthworksTool</c>).
    /// </summary>
    public static class Earthworks
    {
        /// <summary>Prijs per kubieke meter die wordt afgegraven of opgehoogd.</summary>
        public const float PricePerCubicMeter = 12f;

        /// <summary>Kosten en haalbaarheid van <paramref name="cut"/> afgraven en <paramref name="fill"/> ophogen met dit depot.</summary>
        public static EarthworksQuote Quote(float cut, float fill, SoilDepot depot)
        {
            cut = Math.Max(0f, cut);
            fill = Math.Max(0f, fill);
            return new EarthworksQuote(cut, fill, (cut + fill) * PricePerCubicMeter, Check(fill - cut, depot));
        }

        /// <summary>Boekt het netto verschil in het depot; doet niets als de streek niet kan.</summary>
        public static void Commit(in EarthworksQuote quote, SoilDepot depot)
        {
            if (quote.Feasible && depot != null)
            {
                depot.Move(quote.Net);
            }
        }

        /// <summary>Tekst voor de preview naast het penseel.</summary>
        public static string Describe(in EarthworksQuote quote) => quote.Block switch
        {
            EarthworksBlock.NoDepot => "Geen bron of put",
            EarthworksBlock.NotEnoughSoil => "Te weinig grond in de bron",
            EarthworksBlock.DepotFull => "Put zit vol",
            _ => $"{quote.Cut:0} m³ af, {quote.Fill:0} m³ erbij, kosten {quote.Cost:0}",
        };

        private static EarthworksBlock Check(float net, SoilDepot depot)
        {
            if (net > 0f)
            {
                if (depot == null)
                {
                    return EarthworksBlock.NoDepot;
                }

                return net > depot.Stock ? EarthworksBlock.NotEnoughSoil : EarthworksBlock.None;
            }

            if (net < 0f)
            {
                if (depot == null)
                {
                    return EarthworksBlock.NoDepot;
                }

                return -net > depot.Space ? EarthworksBlock.DepotFull : EarthworksBlock.None;
            }

            return EarthworksBlock.None;
        }
    }
}
