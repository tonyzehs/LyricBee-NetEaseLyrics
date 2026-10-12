#if LYRICBEE_DEV_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MusicBeePlugin
{
    internal enum DiagnosticKind
    {
        RequestStarted, RequestSuccess, RequestFailure, RequestCancelled, RequestLimit,
        SearchBudgetExhausted, LookupBudgetExhausted, MatchingSelected, MatchingAmbiguous,
        MatchingNoEligible, CompatibleGroup, RemasterNote, VersionNote, SoundtrackNote,
        TrackPrefixNote, Custom10Override, RetryCompatibleListing, ExtrasRetryLimit,
        InstrumentalListing, OriginalMissing, AuxiliaryOutput, CachedMandarin, NoTitle, LookupException
    }
    internal enum DiagnosticLookupStatus { Running, Lyrics, NoLyrics, Instrumental, Error }

    // No arbitrary trace strings, tags, URLs, paths, response bodies or exception messages enter this API.
    internal sealed class DevelopmentDiagnostics
    {
        internal const int MaximumLookups = 5, MaximumEventsPerLookup = 40, MaximumReportBytes = 32768;
        private readonly object _gate = new object();
        private readonly Queue<DiagnosticLookup> _history = new Queue<DiagnosticLookup>();
        private long _sequence;
        internal bool HasLookups { get { lock (_gate) return _history.Count > 0; } }

        internal DiagnosticLookup Begin(NeteaseConfig config)
        {
            lock (_gate)
            {
                while (_history.Count >= MaximumLookups) _history.Dequeue().Discard();
                var lookup = new DiagnosticLookup(this, ++_sequence, config);
                _history.Enqueue(lookup);
                return lookup;
            }
        }

        internal void Clear()
        {
            lock (_gate)
            {
                foreach (var lookup in _history) lookup.Discard();
                _history.Clear();
            }
        }

        internal string Report()
        {
            lock (_gate)
            {
                var report = new StringBuilder("LyricBee DEV BUILD diagnostic report\n");
                report.Append("Build: ").Append(Plugin.BuildVersion).Append("\nRuntime: ")
                    .Append(Environment.Version).Append(Environment.Is64BitProcess ? " x64\n" : " x86\n");
                report.Append("NetEase song IDs can reveal listening history. No lyrics, tags, paths, URLs or account data included.\n");
                report.Append("History: ").Append(_history.Count).Append("/5 lookups; at most 40 events each.\n");
                foreach (var lookup in _history) report.Append(lookup.Render());
                // ASCII-only structured fields allow an exact byte cap without breaking Unicode text.
                var text = report.ToString();
                const string truncated = "\n[report truncated]\n";
                return Encoding.UTF8.GetByteCount(text) <= MaximumReportBytes ? text :
                    text.Substring(0, MaximumReportBytes - truncated.Length) + truncated;
            }
        }

        internal sealed class DiagnosticLookup
        {
            private readonly DevelopmentDiagnostics _owner;
            private readonly long _sequence;
            private readonly DateTime _started = DateTime.UtcNow;
            private readonly Stopwatch _elapsed = Stopwatch.StartNew();
            private readonly string _settings;
            private readonly Queue<DiagnosticEvent> _events = new Queue<DiagnosticEvent>();
            private DiagnosticLookupStatus _status;
            private bool _discarded;
            private int _dropped, _searches, _lyrics;
            private long _duration;

            internal DiagnosticLookup(DevelopmentDiagnostics owner, long sequence, NeteaseConfig config)
            {
                _owner = owner; _sequence = sequence;
                _settings = "default=" + config.DefaultContent + "; separate=" + config.UseSeparateLines +
                    "; spelling=" + config.AllowSpelling + "; http=" + config.AllowInsecureHttp +
                    "; JP=" + Choice(config.JapaneseOverride) + "; KR=" + Choice(config.KoreanOverride) +
                    "; Cantonese=" + Choice(config.CantoneseOverride) + "; English=" + Choice(config.EnglishOverride);
            }
            private static string Choice(NeteaseConfig.AccompanyingText? choice) => choice.HasValue ? choice.Value.ToString() : "Default";

            internal void Add(DiagnosticKind kind, RequestInterface api = RequestInterface.Other,
                RequestOutcome outcome = RequestOutcome.None, int code = 0, long songId = 0, int count = 0,
                int searches = -1, int lyrics = -1)
            {
                lock (_owner._gate)
                {
                    if (_discarded) return;
                    if (searches >= 0) _searches = searches;
                    if (lyrics >= 0) _lyrics = lyrics;
                    if (_events.Count >= MaximumEventsPerLookup) { _events.Dequeue(); _dropped++; }
                    _events.Enqueue(new DiagnosticEvent { Kind = kind, Api = api, Outcome = outcome, Code = code,
                        SongId = songId, Count = count, Milliseconds = _elapsed.ElapsedMilliseconds });
                }
            }

            internal void Complete(DiagnosticLookupStatus status)
            {
                lock (_owner._gate)
                {
                    if (_discarded) return;
                    _status = status; _duration = _elapsed.ElapsedMilliseconds;
                }
            }

            internal void Discard() { _discarded = true; _events.Clear(); }
            internal string Render()
            {
                var text = new StringBuilder("\nLookup ").Append(_sequence).Append(" ").Append(_started.ToString("o", CultureInfo.InvariantCulture))
                    .Append(" status=").Append(_status).Append(" elapsed_ms=")
                    .Append(_status == DiagnosticLookupStatus.Running ? _elapsed.ElapsedMilliseconds : _duration)
                    .Append(" search_requests=").Append(_searches).Append(" lyric_phase_requests=").Append(_lyrics)
                    .Append(" dropped_events=").Append(_dropped).Append('\n').Append(_settings).Append('\n');
                foreach (var entry in _events) text.Append(entry.Render()).Append('\n');
                return text.ToString();
            }
        }

        private sealed class DiagnosticEvent
        {
            internal DiagnosticKind Kind;
            internal RequestInterface Api;
            internal RequestOutcome Outcome;
            internal int Code, Count;
            internal long SongId, Milliseconds;
            internal string Render() => Milliseconds.ToString(CultureInfo.InvariantCulture) + "ms " + Kind +
                " interface=" + Api + " outcome=" + Outcome + " code=" + Code.ToString(CultureInfo.InvariantCulture) +
                " song_id=" + SongId.ToString(CultureInfo.InvariantCulture) + " count=" + Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
#endif
