using System;

namespace Cities.Gameplay.Tools
{
    /// <summary>Houdt de actieve tool bij, met zijn preview, en beslist wat klikken en annuleren doen.</summary>
    public sealed class ToolController
    {
        public ITool Active { get; private set; }

        public ToolPreview Preview { get; private set; }

        public event Action<ITool> ActiveChanged;

        /// <summary>Maakt <paramref name="tool"/> actief; <c>null</c> zet de tools uit.</summary>
        public void Select(ITool tool)
        {
            if (ReferenceEquals(tool, Active))
            {
                return;
            }

            Active?.Deactivate();
            Active = tool;
            Preview = ToolPreview.Hidden;
            Active?.Activate();
            ActiveChanged?.Invoke(Active);
        }

        public void Deselect() => Select(null);

        public void Update(in ToolInput input) => Preview = Active?.UpdatePreview(input) ?? ToolPreview.Hidden;

        /// <summary>Voert de preview uit als die geldig is.</summary>
        public bool Apply()
        {
            if (Active == null || !Preview.IsValid)
            {
                return false;
            }

            Active.Apply(Preview);
            return true;
        }

        /// <summary>
        /// Escape: eerst een lopende stap afbreken, anders de tool uitzetten. <c>false</c> als er geen tool actief
        /// was, zodat de toets dan het menu opent.
        /// </summary>
        public bool Cancel()
        {
            if (Active == null)
            {
                return false;
            }

            if (!Active.CancelStep())
            {
                Deselect();
            }

            return true;
        }
    }
}
