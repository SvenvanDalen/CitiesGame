using System;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    public enum NoiseKind
    {
        Road,
        Industry,
        Airport,
    }

    /// <summary>Een geluidsbron op een plek in de wereld (meters); <see cref="Shielded"/> betekent dat een geluidswand de bron afschermt.</summary>
    public readonly struct NoiseSource
    {
        public NoiseSource(float2 position, NoiseKind kind, bool shielded = false)
        {
            Position = position;
            Kind = kind;
            Shielded = shielded;
        }

        public float2 Position { get; }

        public NoiseKind Kind { get; }

        public bool Shielded { get; }
    }

    /// <summary>
    /// Geluidsvervuiling (E12-04) in decibel. Geluid dooft niet met diffusie uit maar met afstand: elke bron geeft op 1 m een vast niveau
    /// (<see cref="SourceLevel"/>) dat met 20 log10(afstand) afneemt, een geluidswand haalt er <see cref="BarrierReduction"/> af, en
    /// meerdere bronnen tellen op als energie. Een stille cel heeft het <see cref="Background"/>niveau. <see cref="Fill"/> vult een
    /// celkaart; <see cref="Annoyance"/> zet decibel om naar 0 (rustig) tot 1 (ondraaglijk) voor welzijn en landwaarde.
    /// </summary>
    /// <remarks>
    /// shortcut: elke bron is een puntbron (een weg telt als één punt per wegsegment) en een wand dempt vlak, ongeacht hoek of hoogte;
    /// upgrade naar lijnbronnen (10 log10) en een wandhoogte-afhankelijke demping als wegen te onrealistisch klinken.
    /// </remarks>
    public static class NoiseModel
    {
        /// <summary>Niveau van een rustig woongebied in dB.</summary>
        public const float Background = 30f;

        /// <summary>Demping door een geluidswand in dB (realistisch 5 tot 15).</summary>
        public const float BarrierReduction = 10f;

        /// <summary>Onder deze afstand in meter wordt er niet verder opgelopen, zodat een bron op de cel zelf eindig blijft.</summary>
        public const float MinDistance = 1f;

        /// <summary>Gemiddeld geluidsniveau op 1 m van de bron in dB.</summary>
        public static float SourceLevel(NoiseKind kind) => kind switch
        {
            NoiseKind.Road => 75f,
            NoiseKind.Industry => 95f,
            NoiseKind.Airport => 120f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende geluidsbron."),
        };

        /// <summary>Het niveau van één bron op <paramref name="distance"/> meter, zonder achtergrond (kan ver weg onder 0 dB komen).</summary>
        public static float Level(NoiseKind kind, float distance, bool shielded)
        {
            return SourceLevel(kind) - (20f * math.log10(math.max(distance, MinDistance))) - (shielded ? BarrierReduction : 0f);
        }

        /// <summary>Telt niveaus in dB op als energie: twee gelijke bronnen zijn 3 dB luider, niet het dubbele.</summary>
        public static float Combine(float a, float b) => 10f * math.log10(math.pow(10f, a / 10f) + math.pow(10f, b / 10f));

        /// <summary>0 onder 45 dB (rustig), 1 vanaf 85 dB (gehoorschade), daartussen lineair.</summary>
        public static float Annoyance(float decibel) => math.saturate((decibel - 45f) / 40f);

        /// <summary>
        /// Vult <paramref name="map"/> (rij voor rij, <paramref name="width"/> bij <paramref name="height"/> cellen van
        /// <paramref name="cellSize"/> meter, celmiddens) met het totale niveau in dB.
        /// </summary>
        public static void Fill(float[] map, int width, int height, float cellSize, NoiseSource[] sources)
        {
            if (map is null || sources is null || width < 1 || height < 1 || !(cellSize > 0f) || map.Length != width * height)
            {
                throw new ArgumentException("De kaart moet width * height cellen hebben en cellSize moet groter dan nul zijn.");
            }

            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    map[(z * width) + x] = LevelAt(new float2((x + 0.5f) * cellSize, (z + 0.5f) * cellSize), sources);
                }
            }
        }

        public static float LevelAt(float2 point, NoiseSource[] sources)
        {
            var total = Background;
            foreach (var s in sources)
            {
                total = Combine(total, Level(s.Kind, math.distance(point, s.Position), s.Shielded));
            }

            return total;
        }
    }
}
