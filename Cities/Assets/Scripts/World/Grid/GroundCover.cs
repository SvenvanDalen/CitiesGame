namespace Cities.World.Grid
{
    /// <summary>
    /// Wat de mens van de grond maakt, per cel. Natuurlijke grond kiest zelf gras, zand of rots uit helling en hoogte;
    /// akkers en bebouwde grond worden door boerderijen, gebouwen en wegen gezet.
    /// </summary>
    public enum GroundCover : byte
    {
        Natural = 0,
        Field = 1,
        Built = 2,
    }
}
