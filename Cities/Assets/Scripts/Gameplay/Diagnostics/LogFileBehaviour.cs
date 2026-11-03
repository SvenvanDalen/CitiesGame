using System;
using System.IO;
using Cities.Core.Diagnostics;
using UnityEngine;

namespace Cities.Gameplay.Diagnostics
{
    /// <summary>
    /// Zet bij het opstarten een logbestand aan (<c>Application.persistentDataPath/Logs</c>) en schrijft onverwerkte
    /// uitzonderingen erin voordat het spel crasht. Eén per scene is genoeg.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LogFileBehaviour : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maxFiles = FileLogSink.DefaultMaxFiles;

        private FileLogSink _sink;
        private ILogSink _previous;

        public static string LogsDirectory => Path.Combine(Application.persistentDataPath, "Logs");

        private void Awake()
        {
            _previous = SystemLog.Sink;
            _sink = new FileLogSink(LogsDirectory, _previous, _maxFiles);
            SystemLog.Sink = _sink;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDestroy()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            if (_sink != null)
            {
                SystemLog.Sink = _previous ?? new UnityLogSink();
                _sink.Dispose();
                _sink = null;
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                _sink?.WriteFatal("Crash", exception);
            }
        }
    }
}
