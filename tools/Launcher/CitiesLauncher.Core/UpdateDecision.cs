namespace CitiesLauncher.Core;

public enum UpdateAction
{
    /// <summary>Start het spel zoals het is.</summary>
    None,

    /// <summary>Download en installeer de nieuwe release.</summary>
    Update,
}

public sealed record UpdateDecision(UpdateAction Action, string Reason, ReleaseInfo? Release = null)
{
    /// <summary>Update alleen als de release strikt nieuwer is dan de lokale versie. Zonder (leesbare) lokale versie wordt bijgewerkt.</summary>
    public static UpdateDecision Decide(GameVersion? local, ReleaseInfo? latest)
    {
        if (latest is null)
        {
            return new UpdateDecision(UpdateAction.None, "Geen release-informatie beschikbaar.");
        }

        if (local is null)
        {
            return new UpdateDecision(UpdateAction.Update, "Geen lokale versie gevonden.", latest);
        }

        return latest.Version > local
            ? new UpdateDecision(UpdateAction.Update, $"Nieuwere versie {latest.Version} (lokaal {local}).", latest)
            : new UpdateDecision(UpdateAction.None, $"Versie {local} is actueel.");
    }
}
