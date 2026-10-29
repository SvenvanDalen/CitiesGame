using System;
using System.Collections.Generic;

namespace Cities.Simulation.Commands
{
    /// <summary>
    /// Enige weg waarlangs tools de simulatie wijzigen. Tools valideren voor hun preview met
    /// <see cref="Validate{TCommand}"/> en dienen in met <see cref="Enqueue{TCommand}"/>; de simulatie voert de wachtrij
    /// uit met <see cref="ExecutePending"/> aan het begin van een tick (en elk frame tijdens pauze, zodat bouwen
    /// in pauze direct zichtbaar is).
    /// </summary>
    /// <remarks>
    /// Commands worden in volgorde van indienen uitgevoerd en vlak voor uitvoeren opnieuw gevalideerd, omdat de wereld
    /// sinds het indienen veranderd kan zijn. Commands die een handler tijdens het uitvoeren indient, wachten op de
    /// volgende aanroep; zo blijft de volgorde deterministisch. Alleen voor gebruik op de main thread.
    /// </remarks>
    public sealed class CommandBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();
        private List<IPendingCommand> _pending = new List<IPendingCommand>();
        private List<IPendingCommand> _executing = new List<IPendingCommand>();
        private long _nextSequence = 1;

        /// <summary>Na elk verwerkt command, uitgevoerd of afgewezen.</summary>
        public event Action<CommandRecord> CommandProcessed;

        public int PendingCount => _pending.Count;

        public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ISimulationCommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.ContainsKey(typeof(TCommand)))
            {
                throw new InvalidOperationException($"Er is al een handler voor {typeof(TCommand).Name}.");
            }

            _handlers.Add(typeof(TCommand), handler);
        }

        public bool HasHandler<TCommand>() where TCommand : ISimulationCommand => _handlers.ContainsKey(typeof(TCommand));

        /// <summary>Valideert zonder iets te wijzigen; voor previews en tooltips.</summary>
        public CommandValidation Validate<TCommand>(in TCommand command) where TCommand : ISimulationCommand =>
            GetHandler<TCommand>().Validate(command);

        /// <summary>Zet een command in de wachtrij voor de volgende verwerking.</summary>
        /// <returns>Het volgnummer, terug te vinden in <see cref="CommandRecord.Sequence"/>.</returns>
        public long Enqueue<TCommand>(TCommand command) where TCommand : ISimulationCommand
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var handler = GetHandler<TCommand>();
            var sequence = _nextSequence++;
            _pending.Add(new PendingCommand<TCommand>(sequence, command, handler));
            return sequence;
        }

        /// <summary>Voert alle commands uit die vóór deze aanroep zijn ingediend.</summary>
        /// <returns>Het aantal uitgevoerde (niet afgewezen) commands.</returns>
        public int ExecutePending(long tick)
        {
            if (_executing.Count > 0)
            {
                throw new InvalidOperationException("ExecutePending mag niet vanuit een handler worden aangeroepen.");
            }

            (_pending, _executing) = (_executing, _pending);
            var executed = 0;
            try
            {
                foreach (var pending in _executing)
                {
                    var record = pending.Process(tick);
                    if (record.Outcome == CommandOutcome.Executed)
                    {
                        executed++;
                    }

                    CommandProcessed?.Invoke(record);
                }
            }
            finally
            {
                _executing.Clear();
            }

            return executed;
        }

        /// <summary>Gooit de wachtrij leeg zonder uit te voeren, bijvoorbeeld bij het laden van een andere stad.</summary>
        public void ClearPending() => _pending.Clear();

        private ICommandHandler<TCommand> GetHandler<TCommand>() where TCommand : ISimulationCommand
        {
            if (!_handlers.TryGetValue(typeof(TCommand), out var handler))
            {
                throw new InvalidOperationException($"Geen handler geregistreerd voor {typeof(TCommand).Name}.");
            }

            return (ICommandHandler<TCommand>)handler;
        }

        private interface IPendingCommand
        {
            CommandRecord Process(long tick);
        }

        private sealed class PendingCommand<TCommand> : IPendingCommand where TCommand : ISimulationCommand
        {
            private readonly long _sequence;
            private readonly TCommand _command;
            private readonly ICommandHandler<TCommand> _handler;

            public PendingCommand(long sequence, TCommand command, ICommandHandler<TCommand> handler)
            {
                _sequence = sequence;
                _command = command;
                _handler = handler;
            }

            public CommandRecord Process(long tick)
            {
                var validation = _handler.Validate(_command);
                if (!validation.IsValid)
                {
                    return new CommandRecord(_sequence, tick, _command, CommandOutcome.Rejected, validation);
                }

                _handler.Execute(_command);
                return new CommandRecord(_sequence, tick, _command, CommandOutcome.Executed, validation);
            }
        }
    }
}
