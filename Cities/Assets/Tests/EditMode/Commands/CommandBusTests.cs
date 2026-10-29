using System;
using System.Collections.Generic;
using Cities.Simulation.Commands;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Commands
{
    public class CommandBusTests
    {
        private static readonly int[] OnlyCellOne = { 1 };
        private static readonly int[] CellsOneAndTwo = { 1, 2 };

        private struct BuildCommand : ISimulationCommand
        {
            public int Cell;
            public long Cost;
        }

        private struct DemolishCommand : ISimulationCommand
        {
            public int Cell;
        }

        /// <summary>Minimale wereld: een rij cellen en een kas.</summary>
        private sealed class FakeWorld
        {
            public readonly HashSet<int> Built = new HashSet<int>();
            public long Funds = 100;
        }

        private sealed class BuildHandler : ICommandHandler<BuildCommand>
        {
            private readonly FakeWorld _world;

            public BuildHandler(FakeWorld world) => _world = world;

            public Action<BuildCommand> OnExecute { get; set; }

            public CommandValidation Validate(in BuildCommand command)
            {
                if (_world.Built.Contains(command.Cell))
                {
                    return CommandValidation.Invalid("cell-occupied", command.Cost);
                }

                return command.Cost > _world.Funds
                    ? CommandValidation.Invalid("insufficient-funds", command.Cost)
                    : CommandValidation.Valid(command.Cost);
            }

            public void Execute(in BuildCommand command)
            {
                _world.Built.Add(command.Cell);
                _world.Funds -= command.Cost;
                OnExecute?.Invoke(command);
            }
        }

        private sealed class DemolishHandler : ICommandHandler<DemolishCommand>
        {
            private readonly FakeWorld _world;

            public DemolishHandler(FakeWorld world) => _world = world;

            public CommandValidation Validate(in DemolishCommand command) =>
                _world.Built.Contains(command.Cell) ? CommandValidation.Valid(-5) : CommandValidation.Invalid("nothing-to-demolish");

            public void Execute(in DemolishCommand command)
            {
                _world.Built.Remove(command.Cell);
                _world.Funds += 5;
            }
        }

        private FakeWorld _world;
        private BuildHandler _build;
        private CommandBus _bus;
        private List<CommandRecord> _records;

        [SetUp]
        public void SetUp()
        {
            _world = new FakeWorld();
            _build = new BuildHandler(_world);
            _bus = new CommandBus();
            _bus.RegisterHandler(_build);
            _bus.RegisterHandler(new DemolishHandler(_world));
            _records = new List<CommandRecord>();
            _bus.CommandProcessed += _records.Add;
        }

        [Test]
        public void ValidateHasNoSideEffects()
        {
            var validation = _bus.Validate(new BuildCommand { Cell = 1, Cost = 40 });

            Assert.That(validation, Is.EqualTo(CommandValidation.Valid(40)));
            Assert.That(_world.Built, Is.Empty);
            Assert.That(_bus.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void EnqueuedCommandsWaitForExecutePending()
        {
            _bus.Enqueue(new BuildCommand { Cell = 1, Cost = 10 });

            Assert.That(_world.Built, Is.Empty);
            Assert.That(_bus.PendingCount, Is.EqualTo(1));

            var executed = _bus.ExecutePending(7);

            Assert.That(executed, Is.EqualTo(1));
            Assert.That(_world.Built, Is.EquivalentTo(OnlyCellOne));
            Assert.That(_world.Funds, Is.EqualTo(90));
            Assert.That(_records[0].Tick, Is.EqualTo(7));
            Assert.That(_records[0].Outcome, Is.EqualTo(CommandOutcome.Executed));
        }

        [Test]
        public void CommandsAreRevalidatedAtExecution()
        {
            // Beide zijn geldig bij indienen, maar na de eerste is de kas te leeg voor de tweede.
            Assert.That(_bus.Validate(new BuildCommand { Cell = 1, Cost = 70 }).IsValid, Is.True);
            Assert.That(_bus.Validate(new BuildCommand { Cell = 2, Cost = 70 }).IsValid, Is.True);
            _bus.Enqueue(new BuildCommand { Cell = 1, Cost = 70 });
            _bus.Enqueue(new BuildCommand { Cell = 2, Cost = 70 });

            var executed = _bus.ExecutePending(1);

            Assert.That(executed, Is.EqualTo(1));
            Assert.That(_records[1].Outcome, Is.EqualTo(CommandOutcome.Rejected));
            Assert.That(_records[1].Validation.Reason, Is.EqualTo("insufficient-funds"));
            Assert.That(_world.Funds, Is.EqualTo(30));
        }

        [Test]
        public void CommandsRunInSubmissionOrderAcrossTypes()
        {
            var first = _bus.Enqueue(new BuildCommand { Cell = 3, Cost = 10 });
            var second = _bus.Enqueue(new DemolishCommand { Cell = 3 });
            var third = _bus.Enqueue(new DemolishCommand { Cell = 3 });

            _bus.ExecutePending(1);

            Assert.That(_records.ConvertAll(r => r.Sequence), Is.EqualTo(new List<long> { first, second, third }));
            Assert.That(_records.ConvertAll(r => r.Outcome), Is.EqualTo(new List<CommandOutcome>
            {
                CommandOutcome.Executed,
                CommandOutcome.Executed,
                CommandOutcome.Rejected,
            }));
            Assert.That(_world.Funds, Is.EqualTo(95));
        }

        [Test]
        public void CommandsEnqueuedByHandlersWaitForNextBatch()
        {
            _build.OnExecute = c =>
            {
                if (c.Cell == 1)
                {
                    _bus.Enqueue(new BuildCommand { Cell = 2, Cost = 1 });
                }
            };
            _bus.Enqueue(new BuildCommand { Cell = 1, Cost = 1 });

            Assert.That(_bus.ExecutePending(1), Is.EqualTo(1));
            Assert.That(_bus.PendingCount, Is.EqualTo(1));
            Assert.That(_bus.ExecutePending(2), Is.EqualTo(1));
            Assert.That(_world.Built, Is.EquivalentTo(CellsOneAndTwo));
        }

        [Test]
        public void ExecutePendingCannotBeNested()
        {
            _build.OnExecute = _ => _bus.ExecutePending(1);
            _bus.Enqueue(new BuildCommand { Cell = 1, Cost = 1 });

            Assert.Throws<InvalidOperationException>(() => _bus.ExecutePending(1));
            Assert.DoesNotThrow(() => _bus.ExecutePending(2), "De bus blijft bruikbaar na een fout in een handler");
        }

        [Test]
        public void ClearPendingDropsQueue()
        {
            _bus.Enqueue(new BuildCommand { Cell = 1, Cost = 1 });

            _bus.ClearPending();

            Assert.That(_bus.ExecutePending(1), Is.EqualTo(0));
            Assert.That(_records, Is.Empty);
        }

        [Test]
        public void MissingHandlerIsReported()
        {
            var bus = new CommandBus();

            Assert.That(bus.HasHandler<BuildCommand>(), Is.False);
            Assert.Throws<InvalidOperationException>(() => bus.Validate(new BuildCommand()));
            Assert.Throws<InvalidOperationException>(() => bus.Enqueue(new BuildCommand()));
        }

        [Test]
        public void HandlerCanOnlyBeRegisteredOnce()
        {
            Assert.That(_bus.HasHandler<BuildCommand>(), Is.True);
            Assert.Throws<InvalidOperationException>(() => _bus.RegisterHandler(new BuildHandler(_world)));
            Assert.Throws<ArgumentNullException>(() => new CommandBus().RegisterHandler<BuildCommand>(null));
        }

        [Test]
        public void SequenceNumbersIncrease()
        {
            var a = _bus.Enqueue(new BuildCommand { Cell = 1 });
            var b = _bus.Enqueue(new BuildCommand { Cell = 2 });

            Assert.That(b, Is.GreaterThan(a));
        }
    }
}
