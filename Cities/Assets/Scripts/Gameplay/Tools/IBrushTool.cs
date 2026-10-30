namespace Cities.Gameplay.Tools
{
    /// <summary>Een tool met een rond penseel; de terreinshader tekent de rand met deze straal.</summary>
    public interface IBrushTool : ITool
    {
        /// <summary>Straal van het penseel in meters.</summary>
        float Radius { get; }
    }
}
