namespace Cities.Simulation.Commands
{
    /// <summary>
    /// Markeert een opdracht van een tool aan de simulatie, zoals bouwen, slopen of upgraden.
    /// Een command bevat alleen data; de bijbehorende <see cref="ICommandHandler{TCommand}"/> valideert en voert uit.
    /// Gebruik bij voorkeur een struct, zodat commands goedkoop te kopiëren en op te slaan zijn.
    /// </summary>
    public interface ISimulationCommand
    {
    }
}
