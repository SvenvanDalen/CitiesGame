using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Scheduling
{
    /// <summary>Toont tijdens het spelen klok, systemen en synchronisatiepunten, met knoppen voor pauze, snelheid en één stap.</summary>
    [CustomEditor(typeof(SimulationHostBehaviour))]
    public sealed class SimulationHostBehaviourEditor : UnityEditor.Editor
    {
        private static readonly SimulationSpeed[] Speeds =
        {
            SimulationSpeed.Paused,
            SimulationSpeed.Normal,
            SimulationSpeed.Fast,
            SimulationSpeed.Fastest,
        };

        private static readonly string[] SpeedLabels = { "Pauze", "1x", "2x", "3x" };

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var host = (SimulationHostBehaviour)target;
            if (!Application.isPlaying || host.Clock == null)
            {
                return;
            }

            DrawClock(host.Clock);
            DrawScheduler(host);
        }

        private static void DrawClock(SimulationClock clock)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Klok", EditorStyles.boldLabel);
            var calendar = clock.Calendar;
            var minutes = (int)(calendar.TimeOfDay * 24 * 60);
            EditorGUILayout.LabelField("Datum", $"{calendar.Date} ({calendar.Date.DayOfWeek}) {minutes / 60:D2}:{minutes % 60:D2}");
            EditorGUILayout.LabelField("Tick", clock.Tick.ToString("N0"));
            if (clock.DroppedTicks > 0)
            {
                EditorGUILayout.HelpBox($"{clock.DroppedTicks} ticks overgeslagen in het laatste frame", MessageType.Warning);
            }

            var current = System.Array.IndexOf(Speeds, clock.Speed);
            var selected = GUILayout.Toolbar(current, SpeedLabels);
            if (selected != current)
            {
                clock.SetSpeed(Speeds[selected]);
            }

            using (new EditorGUI.DisabledScope(!clock.IsPaused))
            {
                if (GUILayout.Button("Eén tick vooruit"))
                {
                    clock.Step();
                }
            }
        }

        private static void DrawScheduler(SimulationHostBehaviour host)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scheduler", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Ingeplande ticks", host.Runner.ScheduledTicks.ToString("N0"));
            EditorGUILayout.LabelField("Synchronisatiepunten", host.Scheduler.SyncPointCount.ToString("N0"));
            EditorGUILayout.LabelField("Wachtende commands", host.Commands.PendingCount.ToString());
            var systems = host.Scheduler.Systems;
            EditorGUILayout.LabelField("Systemen", systems.Count == 0 ? "geen" : systems.Count.ToString());
            using (new EditorGUI.IndentLevelScope())
            {
                for (var i = 0; i < systems.Count; i++)
                {
                    EditorGUILayout.LabelField($"{i + 1}.", systems[i].Name);
                }
            }
        }
    }
}
