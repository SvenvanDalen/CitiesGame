using Cities.Simulation.Commands;

namespace Cities.Gameplay.Tools
{
    /// <summary>Een tool die na <see cref="ITool.Apply"/> kan zeggen hoe de actie ongedaan te maken is.</summary>
    public interface IUndoSource
    {
        /// <summary>De actie van de laatste <see cref="ITool.Apply"/>, of <c>null</c>; wordt maar één keer teruggegeven.</summary>
        UndoAction TakeUndo();
    }
}
