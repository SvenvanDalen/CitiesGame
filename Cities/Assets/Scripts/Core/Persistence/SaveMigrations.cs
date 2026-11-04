using System;
using System.Collections.Generic;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Upgradepad voor oude saves. Een stap zet de ruwe inhoud van één sectie van versie <c>n</c> om naar versie
    /// <c>n + 1</c>; bij het laden wordt de keten van stappen doorlopen tot de versie van het huidige spel, zodat een
    /// sectie-lezer alleen de nieuwste indeling hoeft te kennen. Een sectie zonder stappen blijft zelf oude versies lezen.
    /// </summary>
    public sealed class SaveMigrations
    {
        private readonly Dictionary<(string Section, int From), Func<byte[], byte[]>> _steps =
            new Dictionary<(string, int), Func<byte[], byte[]>>();

        /// <summary>Registreert de stap van <paramref name="fromVersion"/> naar <paramref name="fromVersion"/> + 1.</summary>
        public SaveMigrations Add(string sectionId, int fromVersion, Func<byte[], byte[]> upgrade)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                throw new ArgumentException("Een sectie-id is verplicht.", nameof(sectionId));
            }

            if (fromVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fromVersion), "Versies beginnen bij 1.");
            }

            if (upgrade is null)
            {
                throw new ArgumentNullException(nameof(upgrade));
            }

            if (!_steps.TryAdd((sectionId, fromVersion), upgrade))
            {
                throw new ArgumentException($"Sectie {sectionId} heeft al een stap vanaf versie {fromVersion}.", nameof(fromVersion));
            }

            return this;
        }

        public bool HasStep(string sectionId, int fromVersion) => _steps.ContainsKey((sectionId, fromVersion));

        /// <summary>
        /// Voert de stappen uit van <paramref name="version"/> naar <paramref name="targetVersion"/>. Ontbreekt een stap,
        /// dan stopt de keten en geeft <paramref name="reached"/> de laatst bereikte versie.
        /// </summary>
        public byte[] Upgrade(string sectionId, int version, int targetVersion, byte[] data, out int reached)
        {
            var current = version;
            while (current < targetVersion && _steps.TryGetValue((sectionId, current), out var step))
            {
                try
                {
                    data = step(data) ?? throw new SaveFormatException($"Upgrade van sectie {sectionId} versie {current} gaf geen gegevens.");
                }
                catch (Exception exception) when (!(exception is SaveFormatException))
                {
                    throw new SaveFormatException($"Upgrade van sectie {sectionId} van versie {current} naar {current + 1} mislukte: {exception.Message}", exception);
                }

                current++;
            }

            reached = current;
            return data;
        }
    }
}
