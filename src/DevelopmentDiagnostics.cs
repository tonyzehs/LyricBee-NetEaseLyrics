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
        InstrumentalListing, OriginalMissing, AuxiliaryOutput, CachedMandarin, NoTitle, LookupException,
        ArtistAliasLookup, ArtistAliasCacheHit, ArtistAliasMatch, LocalizedSoundtrackMatch
    }
    internal enum DiagnosticLookupStatus { Running, Lyrics, NoLyrics, Instrumental, Error }

    // No arbitrary trace strings, tags, URLs, paths, response bodies or exception messages enter this API.
    internal sealed class DevelopmentDiagnostics
    {
        internal const int MaximumLookups = 5, MaximumEventsPerLookup = 40, MaximumReportBytes = 32768;
        private readonly object _gate = new object();
        private readonly Queue<DiagnosticLookup> _history = new Queue<DiagnosticLookup>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Func<long> _milliseconds;
        private long _sequence;
        internal DevelopmentDiagnostics(Func<long> milliseconds = null) { _milliseconds = milliseconds ?? (() => _clock.ElapsedMilliseconds); }
        private long Now => _milliseconds();
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
                var report = new StringBuilder("LyricBee diagnostic report\n");
                report.Append("Build: ").Append(Plugin.BuildVersion).Append("\nRuntime: ")
                    .Append(Environment.Version).Append(Environment.Is64BitProcess ? " x64\n" : " x86\n");
                report.Append("NetEase song IDs can reveal listening history. No lyrics, tags, paths, URLs or account data included.\n");
                report.Append("History: ").Append(_history.Count).Append("/5 lookups; at most 40 events each.\n");
                var reportTime = Now;
                foreach (var lookup in _history) report.Append(lookup.Render(reportTime));
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
            private readonly long _started;
            private readonly string _settings;
            private readonly List<DiagnosticEvent> _first = new List<DiagnosticEvent>(20);
            private readonly Queue<DiagnosticEvent> _last = new Queue<DiagnosticEvent>(20);
            private DiagnosticLookupStatus _status;
            private bool _discarded;
            private int _dropped, _searches, _lyrics;
            private long _duration;

            internal DiagnosticLookup(DevelopmentDiagnostics owner, long sequence, NeteaseConfig config)
            {
                _owner = owner; _sequence = sequence; _started = owner.Now;
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
                    var entry = new DiagnosticEvent { Kind = kind, Api = api, Outcome = outcome, Code = code,
                        SongId = songId, Count = count, Milliseconds = Math.Max(0, _owner.Now - _started) };
                    if (_first.Count < 20) _first.Add(entry);
                    else
                    {
                        if (_last.Count >= 20) { _last.Dequeue(); _dropped++; }
                        _last.Enqueue(entry);
                    }
                }
            }

            internal void Complete(DiagnosticLookupStatus status)
            {
                lock (_owner._gate)
                {
                    if (_discarded) return;
                    _status = status; _duration = Math.Max(0, _owner.Now - _started);
                }
            }

            internal void Discard() { _discarded = true; _first.Clear(); _last.Clear(); }
            internal string Render(long reportTime)
            {
                var age = Math.Max(0, reportTime - _started);
                var relative = age >= 60000 ? (age / 60000).ToString(CultureInfo.InvariantCulture) + " min" :
                    (age / 1000).ToString(CultureInfo.InvariantCulture) + " sec";
                var text = new StringBuilder("\nLookup ").Append(_sequence).Append(", ").Append(relative).Append(" before this report")
                    .Append(" status=").Append(_status).Append(" elapsed_ms=")
                    .Append(_status == DiagnosticLookupStatus.Running ? Math.Max(0, reportTime - _started) : _duration)
                    .Append(" search_requests=").Append(_searches).Append(" lyric_phase_requests=").Append(_lyrics)
                    .Append(" dropped_events=").Append(_dropped).Append('\n').Append(_settings).Append('\n');
                foreach (var entry in _first) text.Append(entry.Render()).Append('\n');
                if (_dropped > 0) text.Append("[").Append(_dropped).Append(" events skipped between first 20 and last 20]\n");
                foreach (var entry in _last) text.Append(entry.Render()).Append('\n');
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
