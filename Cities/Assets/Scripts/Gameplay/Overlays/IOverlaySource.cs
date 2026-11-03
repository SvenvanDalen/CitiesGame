using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Wat een overlay kleurt.</summary>
    public enum OverlayTarget : byte
    {
        Cell,
        Building,
        Segment,
    }

    /// <summary>Waarde voor één gebouw of wegstuk: een lint van <see cref="From"/> naar <see cref="To"/> met breedte <see cref="Width"/>.</summary>
    public readonly struct OverlayEntityValue
    {
        public OverlayEntityValue(ulong id, float3 from, float3 to, float width, float value)
        {
            Id = id;
            From = from;
            To = to;
            Width = width;
            Value = value;
        }

        /// <summary>Gepakte ID (<c>EntityId.Packed</c>).</summary>
        public ulong Id { get; }

        public float3 From { get; }

        public float3 To { get; }

        /// <summary>Breedte in meters; bij een gebouw (<see cref="From"/> gelijk aan <see cref="To"/>) de zijde van het vlak.</summary>
        public float Width { get; }

        public float Value { get; }
    }

    /// <summary>Gegevensbron van een overlay. Een bron meldt met <see cref="Version"/> dat zijn gegevens veranderden.</summary>
    public interface IOverlaySource
    {
        /// <summary>Stabiele sleutel, bijvoorbeeld "land-value".</summary>
        string Id { get; }

        string Name { get; }

        OverlayTarget Target { get; }

        OverlayLegend Legend { get; }

        /// <summary>Wordt hoger zodra de gegevens veranderd zijn; de weergave bakt dan opnieuw.</summary>
        long Version { get; }
    }

    /// <summary>Overlay met één waarde per cel.</summary>
    public interface ICellOverlaySource : IOverlaySource
    {
        int CellsX { get; }

        int CellsZ { get; }

        /// <summary>Waarde van een cel; <see cref="float.NaN"/> betekent geen gegevens.</summary>
        float Value(int x, int z);
    }

    /// <summary>Overlay met één waarde per gebouw of wegstuk.</summary>
    public interface IEntityOverlaySource : IOverlaySource
    {
        void Collect(List<OverlayEntityValue> output);
    }
}
