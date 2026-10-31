using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Wat een <see cref="IInspectionSource"/> over een object meldt: een titel en de huidige waarden.</summary>
    public readonly struct InspectionReport
    {
        public InspectionReport(float3 position, string title, params string[] details)
        {
            Position = position;
            Title = title;
            Details = details ?? Array.Empty<string>();
        }

        public float3 Position { get; }

        public string Title { get; }

        public IReadOnlyList<string> Details { get; }

        public override string ToString() => Details.Count == 0 ? Title : $"{Title} · {string.Join(" · ", Details)}";
    }

    /// <summary>
    /// Levert voor één soort object (gebouw, weg, later burger of voertuig) de live gegevens op een plek op de grond.
    /// Nieuwe soorten voegen alleen een bron toe aan de <see cref="InspectTool"/>.
    /// </summary>
    public interface IInspectionSource
    {
        /// <summary><c>false</c> als er op dit punt niets van deze soort is.</summary>
        bool TryInspect(float3 point, out InspectionReport report);
    }

    /// <summary>
    /// De sim-inspector: wijs een object aan en de toolbalk toont zijn gegevens, elk frame opnieuw uit de simulatie.
    /// De bronnen worden op volgorde gevraagd; de eerste die iets vindt, wint.
    /// </summary>
    public sealed class InspectTool : ITool
    {
        private readonly IInspectionSource[] _sources;

        public InspectTool(params IInspectionSource[] sources)
        {
            _sources = sources?.Where(s => s != null).ToArray() ?? throw new ArgumentNullException(nameof(sources));
        }

        public string Name => "Inspecteren";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public void Activate()
        {
            // Geen toestand om te onthouden.
        }

        public void Deactivate()
        {
            // Geen toestand om te onthouden.
        }

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            var point = input.Pick.Ground.Point;
            foreach (var source in _sources)
            {
                if (source.TryInspect(point, out var report))
                {
                    return new ToolPreview(report.Position, 0f, float2.zero, null, report.ToString());
                }
            }

            return ToolPreview.Hidden;
        }

        public void Apply(in ToolPreview preview)
        {
            // Inspecteren verandert niets; de gegevens staan al in de preview.
        }

        public bool CancelStep() => false;
    }
}
