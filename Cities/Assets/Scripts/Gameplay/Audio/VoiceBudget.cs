using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Gameplay.Audio
{
    /// <summary>Een geluidsbron die wil spelen (E20-03, E20-07): wie, hoe belangrijk, hoe ver van de luisteraar en hoe hard hij op zichzelf is.</summary>
    public readonly struct VoiceRequest
    {
        public VoiceRequest(int id, int priority, float distance, float volume = 1f)
        {
            Id = id;
            Priority = priority;
            Distance = distance;
            Volume = volume;
        }

        public int Id { get; }

        /// <summary>Hoger is belangrijker (sirene boven motorgeluid).</summary>
        public int Priority { get; }

        /// <summary>Afstand tot de luisteraar in meter.</summary>
        public float Distance { get; }

        public float Volume { get; }
    }

    /// <summary>De verdeling van een frame: welke bron op welke slot speelt, wie nieuw is en wie moet stoppen.</summary>
    public sealed class VoiceAssignment
    {
        internal VoiceAssignment(IReadOnlyDictionary<int, int> slots, IReadOnlyList<int> started, IReadOnlyList<int> stopped, IReadOnlyList<int> culled)
        {
            Slots = slots;
            Started = started;
            Stopped = stopped;
            Culled = culled;
        }

        /// <summary>Bron-id naar slot (de index van een <c>AudioSource</c> in de pool).</summary>
        public IReadOnlyDictionary<int, int> Slots { get; }

        public IReadOnlyList<int> Started { get; }

        /// <summary>Bronnen die vorig frame speelden en nu stoppen (buiten bereik, verdrongen of verdwenen).</summary>
        public IReadOnlyList<int> Stopped { get; }

        /// <summary>Bronnen die wel hoorbaar zijn maar geen plek kregen.</summary>
        public IReadOnlyList<int> Culled { get; }

        public int Playing => Slots.Count;
    }

    /// <summary>Afstandsdemping van een 3D-bron (E20-03).</summary>
    public static class AudioAttenuation
    {
        /// <summary>Winst van 1 binnen <paramref name="minDistance"/>, lineair afnemend tot 0 op <paramref name="maxDistance"/>, met een zachte (kwadratische) uitloop.</summary>
        public static float Gain(float distance, float minDistance, float maxDistance)
        {
            if (!(minDistance >= 0f) || !(maxDistance > minDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "Het maximum ligt boven het minimum, dat niet negatief is.");
            }

            if (distance <= minDistance)
            {
                return 1f;
            }

            if (distance >= maxDistance)
            {
                return 0f;
            }

            var t = 1f - ((distance - minDistance) / (maxDistance - minDistance));
            return t * t;
        }
    }

    /// <summary>
    /// Het voice-budget (E20-07) en de pool voor voertuig- en treingeluid (E20-03): hooguit <see cref="MaxVoices"/> bronnen spelen tegelijk.
    /// Bronnen buiten <see cref="MaxDistance"/> tellen niet mee; van de rest winnen de hoogste prioriteit en daarna de hoorbaarste (volume na
    /// afstandsdemping). Een bron die al speelt houdt zijn slot, zodat er geen sprongen zijn; slots van gestopte bronnen worden hergebruikt.
    /// </summary>
    public sealed class VoiceBudget
    {
        private readonly Dictionary<int, int> _slots = new Dictionary<int, int>();

        public VoiceBudget(int maxVoices, float minDistance, float maxDistance)
        {
            if (maxVoices < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxVoices), maxVoices, "Minstens één tegelijk.");
            }

            if (!(minDistance >= 0f) || !(maxDistance > minDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "Het maximum ligt boven het minimum.");
            }

            MaxVoices = maxVoices;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
        }

        public int MaxVoices { get; }

        public float MinDistance { get; }

        public float MaxDistance { get; }

        public int Playing => _slots.Count;

        /// <summary>De hoorbaarheid van een verzoek: eigen volume maal afstandsdemping.</summary>
        public float Audibility(VoiceRequest request) => request.Volume * AudioAttenuation.Gain(request.Distance, MinDistance, MaxDistance);

        /// <summary>Verdeelt de slots voor dit frame over de verzoeken.</summary>
        public VoiceAssignment Update(IEnumerable<VoiceRequest> requests)
        {
            if (requests is null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            var audible = requests.GroupBy(r => r.Id).Select(g => g.First()).Where(r => Audibility(r) > 0f).ToList();
            var winners = audible
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(Audibility)
                .ThenBy(r => r.Id)
                .Take(MaxVoices)
                .ToList();
            var winnerIds = new HashSet<int>(winners.Select(w => w.Id));

            var stopped = _slots.Keys.Where(id => !winnerIds.Contains(id)).OrderBy(id => id).ToList();
            foreach (var id in stopped)
            {
                _slots.Remove(id);
            }

            var used = new HashSet<int>(_slots.Values);
            var started = new List<int>();
            var nextFree = 0;
            foreach (var winnerId in winners.Where(w => !_slots.ContainsKey(w.Id)).Select(w => w.Id).OrderBy(id => id))
            {
                while (used.Contains(nextFree))
                {
                    nextFree++;
                }

                _slots[winnerId] = nextFree;
                used.Add(nextFree);
                started.Add(winnerId);
            }

            var culled = audible.Where(r => !winnerIds.Contains(r.Id)).Select(r => r.Id).OrderBy(id => id).ToList();
            return new VoiceAssignment(new Dictionary<int, int>(_slots), started, stopped, culled);
        }

        /// <summary>Stopt alles, bijvoorbeeld bij het laden van een spel.</summary>
        public void Clear() => _slots.Clear();
    }
}
