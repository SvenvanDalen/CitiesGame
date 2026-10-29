using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cities.Core.Diagnostics
{
    /// <summary>Logniveaus die in de editor zijn in te stellen; de host past deze toe bij het starten.</summary>
    [CreateAssetMenu(fileName = "LogConfiguration", menuName = "Cities/Log configuration")]
    public sealed class LogConfiguration : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Categorie, meestal de systeemnaam.")]
            public string Category;

            public LogLevel Level;
        }

        [SerializeField] private LogLevel _defaultLevel = LogLevel.Info;
        [SerializeField] private List<Entry> _categories = new List<Entry>();

        public void Apply()
        {
            LogLevels.Default = _defaultLevel;
            foreach (var entry in _categories.Where(e => !string.IsNullOrWhiteSpace(e.Category)))
            {
                LogLevels.Set(entry.Category, entry.Level);
            }
        }
    }
}
