using Cities.Simulation.Scheduling;
using Unity.Jobs;

namespace Cities.Simulation.Utilities
{
    /// <summary>Laat een <see cref="UtilityNetwork"/> elke simulatietick rekenen; het net hoort bij de main thread.</summary>
    public sealed class UtilitySystem : ISimulationSystem
    {
        private readonly UtilityNetwork _network;

        public UtilitySystem(string name, UtilityNetwork network)
        {
            Name = name;
            _network = network;
        }

        public string Name { get; }

        public JobHandle Schedule(in SimulationTick tick, JobHandle dependency)
        {
            dependency.Complete();
            _network.Tick();
            return default;
        }
    }
}
