using System.Collections.Generic;

namespace Cities.Gameplay.Cheats
{
    /// <summary>Maakt iets aan in de wereld; de scene levert de uitvoering (gebouwen, voertuigen, burgers).</summary>
    public interface ISpawnService
    {
        /// <summary>De soorten die <see cref="Spawn"/> kent.</summary>
        IReadOnlyList<string> Kinds { get; }

        /// <summary>Zet een <paramref name="kind"/> op wereldpositie (x, z) in meters; geeft een foutmelding of <c>null</c>.</summary>
        string Spawn(string kind, float x, float z);
    }

    /// <summary>Verplaatst de camera naar een punt in de wereld.</summary>
    public interface IViewTeleporter
    {
        /// <summary>Verplaatst naar wereldpositie (x, z) in meters; geeft een foutmelding of <c>null</c>.</summary>
        string TeleportTo(float x, float z);
    }
}
