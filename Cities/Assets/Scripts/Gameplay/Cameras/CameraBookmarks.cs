using System;

namespace Cities.Gameplay.Cameras
{
    /// <summary>Wat <see cref="CameraBookmarks.Use"/> deed.</summary>
    public enum BookmarkOutcome
    {
        Saved,
        Recalled,
        Empty,
    }

    /// <summary>
    /// Vaste bladwijzers voor de camera: elke plek bewaart een stand (<see cref="CameraRig"/>) die je met een sneltoets
    /// terugroept. Wordt opgeslagen via <see cref="CameraBookmarksSection"/>.
    /// </summary>
    public sealed class CameraBookmarks
    {
        public const int Slots = 5;

        private readonly CameraRig?[] _rigs = new CameraRig?[Slots];

        public bool Has(int slot) => _rigs[Check(slot)].HasValue;

        public bool TryGet(int slot, out CameraRig rig)
        {
            var stored = _rigs[Check(slot)];
            rig = stored ?? default;
            return stored.HasValue;
        }

        public void Set(int slot, CameraRig rig) => _rigs[Check(slot)] = rig;

        public void Clear() => Array.Clear(_rigs, 0, Slots);

        /// <summary>Bewaart de huidige doelstand van de camera in <paramref name="slot"/>, of roept hem terug (gedempt).</summary>
        public BookmarkOutcome Use(int slot, bool save, CameraController controller)
        {
            if (save)
            {
                Set(slot, controller.Target);
                return BookmarkOutcome.Saved;
            }

            if (!TryGet(slot, out var rig))
            {
                return BookmarkOutcome.Empty;
            }

            controller.SetTarget(rig);
            return BookmarkOutcome.Recalled;
        }

        private static int Check(int slot) => slot >= 0 && slot < Slots ? slot : throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
