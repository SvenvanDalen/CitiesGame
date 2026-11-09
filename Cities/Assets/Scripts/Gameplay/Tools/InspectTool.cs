using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Soort object dat is aangewezen; de UI kiest er een icoon en kleur mee.</summary>
    public enum InspectionKind
    {
        Other,
        Building,
        Road,
        Junction,
        Citizen,
        Vehicle,
    }

    /// <summary>
    /// Wat een <see cref="IInspectionSource"/> over een object meldt: een titel, de huidige cijfers en de problemen
    /// (bijvoorbeeld 'Dakloos' of 'Vastgelopen'). Problemen staan apart zodat de UI ze kan markeren.
    /// </summary>
    public readonly struct InspectionReport
    {
        public InspectionReport(float3 position, string title, params string[] details)
            : this(InspectionKind.Other, position, title, details, null)
        {
        }

        public InspectionReport(InspectionKind kind, float3 position, string title, IReadOnlyList<string> details, IReadOnlyList<string> problems)
        {
            Kind = kind;
            Position = position;
            Title = title;
            Details = details ?? Array.Empty<string>();
            Problems = problems ?? Array.Empty<string>();
        }

        public InspectionKind Kind { get; }

        public float3 Position { get; }

        public string Title { get; }

        public IReadOnlyList<string> Details { get; }

        public IReadOnlyList<string> Problems { get; }

        public bool HasProblems => Problems.Count > 0;

        public override string ToString()
        {
            var text = Details.Count == 0 ? Title : $"{Title} · {string.Join(" · ", Details)}";
            return HasProblems ? $"{text} · ! {string.Join(", ", Problems)}" : text;
        }
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
        private readonly List<IInspectionSource> _sources;

        public InspectTool(params IInspectionSource[] sources)
        {
            _sources = sources?.Where(s => s != null).ToList() ?? throw new ArgumentNullException(nameof(sources));
        }

        /// <summary>
        /// Voegt een bron toe zodra het systeem erachter bestaat. Agents (burgers, voertuigen) horen vooraan, want ze zijn klein
        /// en liggen boven een weg of gebouw.
        /// </summary>
        public void AddSource(IInspectionSource source, bool first = false)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _sources.Insert(first ? 0 : _sources.Count, source);
        }

        /// <summary>De laatste melding die de tool aan de UI gaf, voor het inspectiepaneel; <c>null</c> als er niets is aangewezen.</summary>
        public InspectionReport? Current { get; private set; }

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
            Current = null;
            if (!input.Pick.HasGround)
            {
                return ToolPreview.Hidden;
            }

            if (Query(input.Pick.Ground.Point, out var report))
            {
                Current = report;
                return new ToolPreview(report.Position, 0f, float2.zero, null, report.ToString());
            }

            return ToolPreview.Hidden;
        }

        /// <summary>Vraagt de bronnen op volgorde wat er op dit punt is; de eerste die iets vindt wint. Werkt ook als de tool niet actief is (infokaart bij de cursor).</summary>
        public bool Query(float3 point, out InspectionReport report)
        {
            foreach (var source in _sources)
            {
                if (source.TryInspect(point, out report))
                {
                    return true;
                }
            }

            report = default;
            return false;
        }

        public void Apply(in ToolPreview preview)
        {
            // Inspecteren verandert niets; de gegevens staan al in de preview.
        }

        public bool CancelStep() => false;
    }
}
