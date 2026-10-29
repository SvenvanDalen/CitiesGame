using Cities.Simulation.Timing;
using UnityEditor;
using UnityEngine;

namespace Cities.Editor.Timing
{
    /// <summary>Toont tijdens het spelen de klokstatus en knoppen voor pauze, snelheid en één stap.</summary>
    [CustomEditor(typeof(SimulationClockBehaviour))]
    public sealed class SimulationClockBehaviourEditor : UnityEditor.Editor
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

            var clock = ((SimulationClockBehaviour)target).Clock;
            if (!Application.isPlaying || clock == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            var calendar = clock.Calendar;
            var minutes = (int)(calendar.TimeOfDay * 24 * 60);
            EditorGUILayout.LabelField("Datum", $"{calendar.Date} ({calendar.Date.DayOfWeek}) {minutes / 60:D2}:{minutes % 60:D2}");
            EditorGUILayout.LabelField("Tick", clock.Tick.ToString("N0"));
            EditorGUILayout.LabelField("Dagen verstreken", calendar.DaysElapsed.ToString("N0"));
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
    }
}
