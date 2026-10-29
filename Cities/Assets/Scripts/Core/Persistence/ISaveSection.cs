using System;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Eén onderdeel van de wereldstatus in een save (klok, kaartlagen, wegennet, ...). Elk systeem met status levert
    /// een sectie; zie <c>docs/decisions/0003-save-formaat.md</c>.
    /// </summary>
    public interface ISaveSection
    {
        /// <summary>Vier ASCII-tekens, uniek in de save, bijvoorbeeld <c>CLCK</c>.</summary>
        string Id { get; }

        /// <summary>Versie van de inhoud; ophogen bij elke wijziging en oude versies blijven lezen.</summary>
        int Version { get; }

        void Write(SaveWriter writer);

        /// <summary>
        /// Leest en controleert de inhoud zonder iets aan het spel te veranderen. Het resultaat past de status toe; het
        /// wordt pas aangeroepen als alle secties van de save goed gelezen zijn.
        /// </summary>
        Action Read(SaveReader reader, int version);
    }
}
