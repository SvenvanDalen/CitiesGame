using UnityEngine;

namespace Cities.Core.Diagnostics
{
    /// <summary>Schrijft naar de Unity-console en het spellog, met de categorie als voorvoegsel: <c>[Traffic] ...</c>.</summary>
    public sealed class UnityLogSink : ILogSink
    {
        public void Write(LogLevel level, string category, string message)
        {
            var line = $"[{category}] {message}";
            switch (level)
            {
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(line);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(line);
                    break;
                default:
                    UnityEngine.Debug.Log(line);
                    break;
            }
        }
    }
}
