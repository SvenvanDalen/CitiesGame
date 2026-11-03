using System;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Gameplay.Cameras
{
    /// <summary>
    /// Sectie <c>CAMB</c>: de camerabladwijzers. Per plek een vlag en, als hij gevuld is, draaipunt, richting, kanteling
    /// en afstand.
    /// </summary>
    public sealed class CameraBookmarksSection : ISaveSection
    {
        public const string SectionId = "CAMB";

        private readonly CameraBookmarks _bookmarks;

        public CameraBookmarksSection(CameraBookmarks bookmarks) => _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(CameraBookmarks.Slots);
            for (var i = 0; i < CameraBookmarks.Slots; i++)
            {
                var has = _bookmarks.TryGet(i, out var rig);
                writer.Write(has);
                if (has)
                {
                    writer.Write(rig.Pivot.x);
                    writer.Write(rig.Pivot.y);
                    writer.Write(rig.Pivot.z);
                    writer.Write(rig.Yaw);
                    writer.Write(rig.Pitch);
                    writer.Write(rig.Distance);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > CameraBookmarks.Slots)
            {
                throw reader.Corrupt($"aantal bladwijzers {count}");
            }

            var rigs = new CameraRig?[count];
            for (var i = 0; i < count; i++)
            {
                if (!reader.ReadBool())
                {
                    continue;
                }

                var pivot = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var yaw = reader.ReadSingle();
                var pitch = reader.ReadSingle();
                var distance = reader.ReadSingle();
                if (!math.all(math.isfinite(pivot)) || !float.IsFinite(yaw) || !float.IsFinite(pitch) || !float.IsFinite(distance) || distance <= 0f)
                {
                    throw reader.Corrupt($"bladwijzer {i} heeft ongeldige waarden");
                }

                rigs[i] = new CameraRig(pivot, yaw, pitch, distance);
            }

            reader.ExpectEnd();
            return () =>
            {
                _bookmarks.Clear();
                for (var i = 0; i < count; i++)
                {
                    if (rigs[i].HasValue)
                    {
                        _bookmarks.Set(i, rigs[i].Value);
                    }
                }
            };
        }
    }
}
