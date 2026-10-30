using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Een bouw- of bewerkingstool die de <see cref="ToolController"/> aanstuurt.</summary>
    public interface ITool
    {
        /// <summary>Naam in de UI.</summary>
        string Name { get; }

        /// <summary>Model voor de ghost, of <c>null</c> voor alleen de footprint.</summary>
        GameObject Ghost { get; }

        /// <summary><c>true</c> als de tool elk frame werkt zolang de knop ingedrukt is (zoals een penseel).</summary>
        bool RepeatsWhileHeld { get; }

        void Activate();

        void Deactivate();

        /// <summary>Werkt de preview bij met de invoer van dit frame.</summary>
        ToolPreview UpdatePreview(in ToolInput input);

        /// <summary>Voert de preview uit; de controller roept dit alleen aan als die geldig is.</summary>
        void Apply(in ToolPreview preview);

        /// <summary>Breekt een lopende stap af (zoals een half getekende weg); <c>false</c> als er niets liep.</summary>
        bool CancelStep();
    }
}
