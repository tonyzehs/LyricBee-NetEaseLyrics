using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicBeePlugin
{
    // Public catalogue metadata only. No song tags, account data or disk writes.
    internal sealed class ArtistAliasCache
    {
        internal const int MaximumArtists = 256;
        internal static readonly ArtistAliasCache Shared = new ArtistAliasCache();
        private readonly object _gate = new object();
        private readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();
        private readonly Func<DateTime> _now;
        private long _sequence;
        internal ArtistAliasCache(Func<DateTime> now = null) { _now = now ?? (() => DateTime.UtcNow); }
        internal int Count { get { lock (_gate) return _entries.Count; } }

        internal bool TryGet(long id, out string[] names)
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(id, out var entry))
                {
                    if (entry.Expires > _now()) { names = (string[])entry.Names.Clone(); return true; }
                    _entries.Remove(id);
                }
                names = null; return false;
            }
        }

        internal void Store(long id, string[] names)
        {
            if (id <= 0) return;
            var bounded = (names ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 256)
                .Take(33).Distinct(StringComparer.Ordinal).ToArray();
            lock (_gate)
            {
                if (!_entries.ContainsKey(id) && _entries.Count >= MaximumArtists)
                    _entries.Remove(_entries.OrderBy(e => e.Value.Sequence).First().Key);
                _entries[id] = new Entry { Names = bounded, Sequence = ++_sequence,
                    Expires = _now().Add(bounded.Length == 0 ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(24)) };
            }
        }
        private sealed class Entry { internal string[] Names; internal DateTime Expires; internal long Sequence; }
    }
}
