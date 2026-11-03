using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Freight.Terminals
{
    /// <summary>Sectie <c>TERM</c>: havens, railterminals, hun opslag en de voertuigen aan de kade (E14-06).</summary>
    public sealed class TerminalSection : ISaveSection
    {
        public const string SectionId = "TERM";

        private readonly TerminalNetwork _network;

        public TerminalSection(TerminalNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_network.Terminals.Count);
            foreach (var terminal in _network.Terminals)
            {
                WriteTerminal(writer, terminal);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > TerminalNetwork.MaxTerminals)
            {
                throw reader.Corrupt($"onmogelijk aantal terminals ({count})");
            }

            var terminals = new List<FreightTerminal>(count);
            for (var i = 0; i < count; i++)
            {
                terminals.Add(ReadTerminal(reader, i));
            }

            reader.ExpectEnd();
            return () => _network.Restore(terminals);
        }

        private static void WriteTerminal(SaveWriter writer, FreightTerminal terminal)
        {
            var spec = terminal.Spec;
            writer.Write((byte)spec.Kind);
            writer.Write((long)terminal.Owner);
            writer.Write(terminal.Position.x);
            writer.Write(terminal.Position.y);
            writer.Write(terminal.Position.z);
            writer.Write(spec.Berths);
            writer.Write(spec.CraneRate);
            writer.Write(spec.GateRate);
            writer.Write(spec.StorageCapacity);
            writer.Write(spec.MaxDockDays);
            writer.Write(terminal.Level);
            writer.Write(terminal.GateBudget);
            writer.Write(terminal.FromCarriers);
            writer.Write(terminal.ToCarriers);
            writer.Write(terminal.FromTrucks);
            writer.Write(terminal.ToTrucks);
            writer.Write(terminal.Refused);
            writer.Write(terminal.DepartedCount);
            writer.Write(terminal.NextCarrierId);
            writer.Write(terminal.Carriers.Count);
            foreach (var carrier in terminal.Carriers)
            {
                writer.Write(carrier.Id);
                writer.Write((byte)carrier.Mode);
                writer.Write(carrier.Capacity);
                writer.Write(carrier.Load);
                writer.Write(carrier.Wanted);
                writer.Write(carrier.Loaded);
                writer.Write(carrier.Unloaded);
                writer.Write((byte)carrier.State);
                writer.Write(carrier.WaitedDays);
                writer.Write(carrier.DockedDays);
            }
        }

        private static FreightTerminal ReadTerminal(SaveReader reader, int id)
        {
            var kind = reader.ReadByte();
            if (!Enum.IsDefined(typeof(TerminalKind), kind))
            {
                throw reader.Corrupt($"onbekend soort terminal {kind}");
            }

            var owner = (ulong)reader.ReadInt64();
            var position = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            TerminalSpec spec;
            try
            {
                spec = new TerminalSpec((TerminalKind)kind, reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            var terminal = new FreightTerminal(id, owner, position, spec)
            {
                Level = reader.ReadSingle(),
                GateBudget = reader.ReadSingle(),
                FromCarriers = reader.ReadSingle(),
                ToCarriers = reader.ReadSingle(),
                FromTrucks = reader.ReadSingle(),
                ToTrucks = reader.ReadSingle(),
                Refused = reader.ReadSingle(),
                DepartedCount = reader.ReadInt32(),
            };
            var next = reader.ReadInt32();
            if (!IsQuantity(terminal.Level) || terminal.Level > spec.StorageCapacity + 0.01f || !IsQuantity(terminal.GateBudget)
                || !IsQuantity(terminal.FromCarriers) || !IsQuantity(terminal.ToCarriers) || !IsQuantity(terminal.FromTrucks)
                || !IsQuantity(terminal.ToTrucks) || !IsQuantity(terminal.Refused) || terminal.DepartedCount < 0 || next < 0)
            {
                throw reader.Corrupt("ongeldige hoeveelheid in een terminal");
            }

            ReadCarriers(reader, terminal, next);
            terminal.RestoreNextCarrierId(next);
            return terminal;
        }

        private static void ReadCarriers(SaveReader reader, FreightTerminal terminal, int next)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > FreightTerminal.MaxCarriers)
            {
                throw reader.Corrupt($"onmogelijk aantal voertuigen ({count})");
            }

            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var mode = reader.ReadByte();
                var capacity = reader.ReadSingle();
                var load = reader.ReadSingle();
                var wanted = reader.ReadSingle();
                var loaded = reader.ReadSingle();
                var unloaded = reader.ReadSingle();
                var state = reader.ReadByte();
                var waited = reader.ReadSingle();
                var docked = reader.ReadSingle();
                if (id < 0 || id >= next || !Enum.IsDefined(typeof(CarrierMode), mode) || (CarrierMode)mode != terminal.Spec.ServedMode
                    || !Enum.IsDefined(typeof(CarrierState), state))
                {
                    throw reader.Corrupt($"ongeldig voertuig {id}");
                }

                if (!(capacity > 0f) || !IsQuantity(load) || load > capacity + 0.01f || !IsQuantity(wanted) || !IsQuantity(loaded)
                    || !IsQuantity(unloaded) || !IsQuantity(waited) || !IsQuantity(docked) || load < loaded - 0.01f)
                {
                    throw reader.Corrupt($"ongeldige lading van voertuig {id}");
                }

                var carrier = terminal.RestoreCarrier(id, (CarrierMode)mode, capacity, load, wanted);
                carrier.Loaded = loaded;
                carrier.Unloaded = unloaded;
                carrier.State = (CarrierState)state;
                carrier.WaitedDays = waited;
                carrier.DockedDays = docked;
            }
        }

        private static bool IsQuantity(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
