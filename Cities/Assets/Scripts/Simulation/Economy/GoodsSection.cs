using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Economy
{
    /// <summary>Sectie <c>GOOD</c>: de voorraden en de beschikbaarheid per goederenketen (E11-08). De parameters van de ketens zelf komen uit de configuratie.</summary>
    public sealed class GoodsSection : ISaveSection
    {
        public const string SectionId = "GOOD";

        private readonly GoodsNetwork _network;

        public GoodsSection(GoodsNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var chains = _network.Chains.ToArray();
            writer.Write(chains.Length);
            foreach (var chain in chains)
            {
                writer.Write(chain.Name);
                writer.Write(chain.RawStock);
                writer.Write(chain.ProductStock);
                writer.Write(chain.Availability);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > GoodsNetwork.MaxChains)
            {
                throw reader.Corrupt($"onmogelijk aantal goederenketens: {count}");
            }

            var states = new List<(GoodsChain Chain, float Raw, float Product, float Availability)>();
            for (var i = 0; i < count; i++)
            {
                states.Add(ReadState(reader));
            }

            reader.ExpectEnd();
            if (states.Select(s => s.Chain.Name).Distinct().Count() != count)
            {
                throw reader.Corrupt("een goederenketen staat twee keer in de opslag");
            }

            return () =>
            {
                foreach (var state in states)
                {
                    state.Chain.Restore(state.Raw, state.Product, state.Availability);
                }
            };
        }

        private (GoodsChain Chain, float Raw, float Product, float Availability) ReadState(SaveReader reader)
        {
            var name = reader.ReadString();
            var raw = reader.ReadSingle();
            var product = reader.ReadSingle();
            var availability = reader.ReadSingle();
            if (!_network.Contains(name))
            {
                throw reader.Corrupt("onbekende goederenketen: " + name);
            }

            if (!(raw >= 0f) || !(product >= 0f) || !(availability >= 0f && availability <= 1f))
            {
                throw reader.Corrupt("ongeldige voorraad voor " + name);
            }

            return (_network.Get(name), raw, product, availability);
        }
    }
}
