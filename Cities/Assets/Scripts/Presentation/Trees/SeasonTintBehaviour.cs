using System.Collections.Generic;
using Cities.Gameplay.Expansion;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Presentation.Trees
{
    /// <summary>
    /// Seizoenskleuren (E19-12): kleurt het gebladerte per dag naar het seizoen. De oorspronkelijke kleuren worden bewaard en bij het
    /// afsluiten hersteld, zodat de materialen in het project niet veranderen.
    /// </summary>
    public sealed class SeasonTintBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private ExpansionHostBehaviour _expansion;
        [SerializeField] private TreeRendererBehaviour _trees;

        private readonly List<(Material Material, int Property, Color Original)> _materials = new List<(Material, int, Color)>();

        private void Start()
        {
            if (_trees == null || _trees.Models == null || _trees.Models.Species == null)
            {
                return;
            }

            foreach (var species in _trees.Models.Species)
            {
                var material = species?.Foliage;
                if (material == null)
                {
                    continue;
                }

                var property = material.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") : Shader.PropertyToID("_Color");
                if (material.HasProperty(property))
                {
                    _materials.Add((material, property, material.GetColor(property)));
                }
            }

            _simulation.Clock.Calendar.DayStarted += OnDay;
            OnDay(_simulation.Clock.Calendar.Date);
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }

            Restore();
        }

        private void OnDay(GameDate date)
        {
            if (!_expansion.Expansion.Settings.Seasons)
            {
                Restore();
                return;
            }

            var dayOfYear = date.ToDateTime().DayOfYear;
            var look = _expansion.Expansion.Seasons;
            var foliage = look.Foliage(dayOfYear, date.Year);
            var autumn = look.AutumnTint(dayOfYear, date.Year);
            foreach (var (material, property, original) in _materials)
            {
                var tint = SeasonTint.Leaf(new float3(original.r, original.g, original.b), foliage, autumn);
                material.SetColor(property, new Color(tint.x, tint.y, tint.z, original.a));
            }
        }

        private void Restore()
        {
            foreach (var (material, property, original) in _materials)
            {
                if (material != null)
                {
                    material.SetColor(property, original);
                }
            }
        }
    }
}
