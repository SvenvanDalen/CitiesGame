namespace Cities.Simulation.Commands
{
    /// <summary>Valideert en voert één soort command uit.</summary>
    public interface ICommandHandler<TCommand> where TCommand : ISimulationCommand
    {
        /// <summary>
        /// Controleert of het command nu uitvoerbaar is en wat het kost. Mag de wereld niet wijzigen:
        /// tools roepen dit elk frame aan voor hun preview.
        /// </summary>
        CommandValidation Validate(in TCommand command);

        /// <summary>Voert een command uit dat zojuist geldig is bevonden.</summary>
        void Execute(in TCommand command);
    }
}
