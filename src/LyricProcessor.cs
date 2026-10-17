using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static partial class LyricProcessor
    {
        internal const int AlignmentToleranceMs = 50;
        private static readonly Regex LeadingTimes = new Regex(
            @"^(?<time>\[(?<minutes>[0-9]+):(?<seconds>[0-5]?[0-9])(?:[.:](?<fraction>[0-9]{1,9}))?\])+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex MetadataLine = new Regex(@"^\[[a-zA-Z][a-zA-Z0-9]*:.*\]$", RegexOptions.CultureInvariant);
        private static readonly Regex SectionLine = new Regex(@"^\[(?![0-9])[^\]\r\n]+\]$", RegexOptions.CultureInvariant);
        private static readonly Regex CreditLine = new Regex(
            @"^(?:作词|作詞|作曲|编曲|編曲|演唱|作詩|歌手|lyrics?|lyricist|composer|composed by|written by|arranger|arrangement|vocals?)\s*[:：]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool IsInstrumentalPlaceholder(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var contents = NormalizeLines(text).Split('\n')
                .Select(line => Regex.Replace(line, @"^(?:\s*\[[^\]]*\])*\s*", ""))
                .Select(line => Regex.Replace(line, @"[\s\p{P}]", ""))
                .Where(line => line.Length > 0).ToArray();
            return contents.Length > 0 && contents.All(line => line == "纯音乐请欣赏" || line == "純音樂請欣賞");
        }

        internal static bool HasLyricText(string text) => !string.IsNullOrWhiteSpace(text) &&
            NormalizeLines(text).Split('\n').Select(line => LeadingTimes.Replace(line.Trim(), "")).Any(IsText);
        private static bool IsText(string line) => !IsHeader(line) && Regex.IsMatch(line, @"[\p{L}\p{N}]");
        private static bool IsHeader(string line) => MetadataLine.IsMatch(line.Trim()) || SectionLine.IsMatch(line.Trim()) || IsCreditHeader(line);
        public static string InjectTranslation(string original, string translation) => MergeTranslation(original, translation, false);
        public static string InjectTranslationTwoLines(string original, string translation) => MergeTranslation(original, translation, true);

        internal static string MergeTranslation(string originalLrc, string translationLrc, bool twoLines, Action<string> trace = null)
        {
            if (!HasLyricText(translationLrc) || string.IsNullOrWhiteSpace(originalLrc)) return originalLrc;
            var original = ReadTimedDocument(originalLrc);
            var translation = ReadTimedDocument(translationLrc);
            if (original.InvalidTimes > 0 && original.Entries.Count == 0) return originalLrc;
            if (original.Entries.Count == 0) return FormatUntimedTranslation(originalLrc, translationLrc);
            if (translation.Entries.Count == 0) return originalLrc;
            var originals = Cues(original);
            var translations = Cues(translation).Where(c => HasLyricText(c.Text)).ToArray();
            var alignment = ChooseAlignment(original, originals, translation, translations, trace);
            var lines = new List<string>(original.Headers);
            var loose = original.LooseLines.ToLookup(l => l.AfterTime);
            AppendLooseText(loose, null, lines);
            for (var index = 0; index < originals.Length; index++)
            {
                var cue = originals[index];
                var content = cue.Text;
                if (HasLyricText(content) && alignment.Pairs.TryGetValue(index, out var pair))
                    content += (twoLines ? "\n" : "/") + translations[pair.Translation].Text;
                // A single timestamp owns the original and its continuation in MusicBee.
                lines.Add(FormatTimestamp(cue.Time) + content);
                AppendLooseText(loose, cue.Time, lines);
            }
            return string.Join("\n", lines);
        }

        private static Alignment ChooseAlignment(TimedDocument original, TimedText[] originals,
            TimedDocument translation, TimedText[] translations, Action<string> trace)
        {
            var adjusted = Align(originals, translations, original.Offset, translation.Offset);
            var raw = Align(originals, translations, 0, 0);
            var alignment = adjusted;
            var rawChosen = Better(raw, adjusted);
            if (rawChosen) alignment = raw;
            else if (SameQuality(raw, adjusted) && !SamePairs(raw, adjusted))
            {
                // Neither clock is supported more strongly. Retain only common exact pairs.
                alignment = new Alignment();
                foreach (var pair in adjusted.Pairs.Where(p => p.Value.Error == 0 && raw.Pairs.ContainsKey(p.Key) &&
                    raw.Pairs[p.Key].Translation == p.Value.Translation && raw.Pairs[p.Key].Error == 0))
                    alignment.Pairs.Add(pair.Key, pair.Value);
                trace?.Invoke("Alignment: ambiguous clocks; retained common exact matches only");
            }
            if (rawChosen || alignment.Pairs.Any(p => p.Value.Error > 0))
                trace?.Invoke("Alignment: " + (rawChosen ? "raw" : "offset-adjusted") + " timing; paired " +
                    alignment.Pairs.Count + " cue groups; maximum error " + alignment.MaxError + " ms (limit 50 ms)");
            if (original.InvalidTimes > 0 || translation.InvalidTimes > 0)
                trace?.Invoke("Lyrics: isolated invalid timing lines from adjacent cues");
            return alignment;
        }

        private sealed class AuxiliaryTrack
        {
            internal TimedText[] Cues;
            internal Alignment Alignment;
            internal bool Used;
        }

        internal static string MergePronunciation(string originalLrc, string pronunciationLrc, string translationLrc,
            bool separateLines, Action<string> trace, out int usableTracks)
        {
            usableTracks = 0;
            if (string.IsNullOrWhiteSpace(originalLrc)) return originalLrc;
            var extraTexts = new[] { pronunciationLrc, translationLrc };
            var original = ReadTimedDocument(originalLrc);
            if (original.InvalidTimes > 0 && original.Entries.Count == 0)
            {
                trace?.Invoke("Lyrics: retained original with invalid timing; no invented alignment");
                return originalLrc;
            }
            if (original.Entries.Count == 0) return FormatUntimedTracks(originalLrc, extraTexts, out usableTracks);
            var originals = Cues(original);
            var tracks = new List<AuxiliaryTrack>();
            for (var extraIndex = 0; extraIndex < extraTexts.Length; extraIndex++)
            {
                var text = extraTexts[extraIndex];
                if (!HasLyricText(text) || IsInstrumentalPlaceholder(text)) continue;
                var document = ReadTimedDocument(text);
                // An untimed auxiliary track cannot borrow a timed original's playback cues.
                if (document.Entries.Count == 0) continue;
                var cues = Cues(document).Where(c => HasLyricText(c.Text)).ToArray();
                var label = extraIndex == 0 ? "Pronunciation: " : "Translation: ";
                tracks.Add(new AuxiliaryTrack { Cues = cues,
                    Alignment = ChooseAlignment(original, originals, document, cues, m => trace?.Invoke(label + m)) });
            }
            if (tracks.All(t => t.Alignment.Pairs.Count == 0)) return originalLrc;
            var lines = new List<string>(original.Headers);
            var loose = original.LooseLines.ToLookup(l => l.AfterTime);
            AppendLooseText(loose, null, lines);
            for (var index = 0; index < originals.Length; index++)
            {
                var cue = originals[index];
                var parts = new List<string> { cue.Text };
                foreach (var track in tracks)
                    if (HasLyricText(cue.Text) && track.Alignment.Pairs.TryGetValue(index, out var pair))
                    {
                        var extra = track.Cues[pair.Translation].Text;
                        if (parts.Any(p => p.Trim() == extra.Trim())) continue;
                        parts.Add(extra); track.Used = true;
                    }
                lines.Add(FormatTimestamp(cue.Time) + string.Join(separateLines ? "\n" : " / ", parts));
                AppendLooseText(loose, cue.Time, lines);
            }
            usableTracks = tracks.Count(t => t.Used);
            trace?.Invoke("Lyrics: retained " + usableTracks + " requested auxiliary tracks; unavailable or unaligned lines omitted");
            return usableTracks == 0 ? originalLrc : string.Join("\n", lines);
        }

        private sealed class UntimedTrack
        {
            internal string[] Lines;
            internal List<string[]> Verses;
            internal bool Positional, Used;
            internal int Cursor;
        }

        private static string FormatUntimedTracks(string originalLrc, string[] extraTexts, out int usableTracks)
        {
            usableTracks = 0;
            var originalLines = NormalizeLines(originalLrc).Split('\n').Select(CreditHeaderForDisplay).ToArray();
            var originalText = originalLines.Where(IsText).ToArray();
            if (originalText.Length == 0) return originalLrc;
            var originalVerses = SplitVerses(originalLines);
            var tracks = new List<UntimedTrack>();
            foreach (var text in extraTexts.Where(t => HasLyricText(t) && !IsInstrumentalPlaceholder(t)))
            {
                var lines = NormalizeLines(text).Split('\n').Select(l => LeadingTimes.Replace(l.Trim(), ""))
                    .Where(l => !IsHeader(l)).ToArray();
                var words = lines.Where(IsText).ToArray();
                if (words.Length == 0 || words.SequenceEqual(originalText.Select(l => l.Trim())) ||
                    tracks.Any(t => t.Lines.SequenceEqual(words))) continue;
                var verses = SplitVerses(lines);
                if (words.Length == originalText.Length || verses.Count == originalVerses.Count)
                    tracks.Add(new UntimedTrack { Lines = words, Verses = verses, Positional = words.Length == originalText.Length });
            }
            if (tracks.Count == 0) return originalLrc;
            if (tracks.All(t => t.Positional))
            {
                var output = originalLines.Select(line =>
                {
                    if (!IsText(line)) return line;
                    var parts = new List<string> { line.Trim() };
                    foreach (var track in tracks)
                    {
                        var extra = track.Lines[track.Cursor++].Trim();
                        if (parts.Contains(extra)) continue;
                        parts.Add(extra); track.Used = true;
                    }
                    return string.Join(" / ", parts);
                }).ToArray();
                usableTracks = tracks.Count(t => t.Used);
                return usableTracks == 0 ? originalLrc : string.Join("\n", output).TrimEnd();
            }
            var result = originalLines.Where(l => MetadataLine.IsMatch(l.Trim()) || CreditLine.IsMatch(l.Trim())).ToList();
            for (var verseIndex = 0; verseIndex < originalVerses.Count; verseIndex++)
            {
                if (verseIndex > 0) result.Add("");
                var verse = originalVerses[verseIndex];
                var count = verse.Count(IsText);
                var extras = tracks.Select(t => t.Positional ? t.Lines.Skip(t.Cursor).Take(count).ToArray() :
                    t.Verses[verseIndex].Where(IsText).ToArray()).ToArray();
                foreach (var track in tracks.Where(t => t.Positional)) track.Cursor += count;
                if (extras.All(e => e.Length == count))
                {
                    var lineIndex = 0;
                    foreach (var line in verse)
                    {
                        if (!IsText(line)) { result.Add(line); continue; }
                        var parts = new List<string> { line };
                        for (var i = 0; i < tracks.Count; i++)
                            if (!parts.Contains(extras[i][lineIndex])) { parts.Add(extras[i][lineIndex]); tracks[i].Used = true; }
                        result.Add(string.Join(" / ", parts)); lineIndex++;
                    }
                }
                else
                {
                    result.AddRange(verse);
                    for (var i = 0; i < tracks.Count; i++)
                    {
                        if (extras[i].Length == 0 || extras[i].SequenceEqual(verse.Where(IsText))) continue;
                        result.Add(""); result.AddRange(extras[i]); tracks[i].Used = true;
                    }
                }
            }
            usableTracks = tracks.Count(t => t.Used);
            return usableTracks == 0 ? originalLrc : string.Join("\n", result);
        }

        private sealed class Pair { internal int Translation; internal long Error; }
        private sealed class Alignment
        {
            internal readonly Dictionary<int, Pair> Pairs = new Dictionary<int, Pair>();
            internal long TotalError => Pairs.Values.Sum(p => p.Error);
            internal long MaxError => Pairs.Count == 0 ? 0 : Pairs.Values.Max(p => p.Error);
        }
        private static bool Better(Alignment a, Alignment b) => a.Pairs.Count > b.Pairs.Count ||
            (a.Pairs.Count == b.Pairs.Count && (a.TotalError < b.TotalError || (a.TotalError == b.TotalError && a.MaxError < b.MaxError)));
        private static bool SameQuality(Alignment a, Alignment b) => !Better(a, b) && !Better(b, a);
        private static bool SamePairs(Alignment a, Alignment b) => a.Pairs.Count == b.Pairs.Count &&
            a.Pairs.All(p => b.Pairs.TryGetValue(p.Key, out var q) && q.Translation == p.Value.Translation);

        private static Alignment Align(TimedText[] originals, TimedText[] translations, int originalOffset, int translationOffset)
        {
            var result = new Alignment();
            var used = new HashSet<int>();
            var useful = originals.Select(c => HasLyricText(c.Text)).ToArray();
            var eligible = Enumerable.Range(0, originals.Length).Where(i => useful[i]).ToArray();
            var times = eligible.Select(i => originals[i].Time - originalOffset).ToArray();
            var exact = translations.Select((c, i) => new { Time = c.Time - translationOffset, Index = i }).ToDictionary(c => c.Time, c => c.Index);
            for (var i = 0; i < originals.Length; i++)
                if (useful[i] && exact.TryGetValue(originals[i].Time - originalOffset, out var j))
                {
                    result.Pairs.Add(i, new Pair { Translation = j }); used.Add(j);
                }
            var proposals = new List<KeyValuePair<int, Pair>>();
            for (var j = 0; j < translations.Length; j++)
            {
                if (used.Contains(j)) continue;
                var time = translations[j].Time - translationOffset;
                var position = Array.BinarySearch(times, time);
                if (position < 0) position = ~position;
                // Sorted cue times need only the two neighbors, not a quadratic scan of the song.
                var candidates = new[] { position - 1, position }.Where(p => p >= 0 && p < times.Length)
                    .Select(p => new { Index = eligible[p], Error = Math.Abs(times[p] - time) })
                    .Where(c => c.Error <= AlignmentToleranceMs).OrderBy(c => c.Error).ToArray();
                if (candidates.Length == 0 || (candidates.Length > 1 && candidates[0].Error == candidates[1].Error)) continue;
                var best = candidates[0];
                // A near-duplicate translation must not be pushed onto a second-nearest cue.
                if (result.Pairs.ContainsKey(best.Index)) continue;
                proposals.Add(new KeyValuePair<int, Pair>(best.Index, new Pair { Translation = j, Error = best.Error }));
            }
            foreach (var group in proposals.GroupBy(p => p.Key))
            {
                var ordered = group.OrderBy(p => p.Value.Error).ToArray();
                if (ordered.Length > 1 && ordered[0].Value.Error == ordered[1].Value.Error) continue;
                var proposal = ordered[0];
                if (result.Pairs.Any(p => (p.Key < proposal.Key && p.Value.Translation >= proposal.Value.Translation) ||
                    (p.Key > proposal.Key && p.Value.Translation <= proposal.Value.Translation))) continue;
                result.Pairs.Add(proposal.Key, proposal.Value);
            }
            // A few coincidental near times do not establish a document's timing alignment.
            var possible = Math.Min(eligible.Length, translations.Length);
            if (result.Pairs.Count * 2 <= possible)
                foreach (var key in result.Pairs.Where(p => p.Value.Error > 0).Select(p => p.Key).ToArray()) result.Pairs.Remove(key);
            return result;
        }

        private static string FormatUntimedTranslation(string originalLrc, string translationLrc)
        {
            var originalLines = NormalizeLines(originalLrc).Split('\n').Select(CreditHeaderForDisplay).ToArray();
            var translatedLines = NormalizeLines(translationLrc).Split('\n').Select(line => LeadingTimes.Replace(line, ""))
                .Where(line => !IsHeader(line)).ToArray();
            var originals = originalLines.Where(IsText).ToArray();
            var translations = translatedLines.Where(IsText).ToArray();
            if (originals.Length == 0 || translations.Length == 0) return originalLrc;
            if (originals.Length == translations.Length)
            {
                var index = 0;
                return string.Join("\n", originalLines.Select(line => IsText(line) ? line.Trim() + " / " + translations[index++].Trim() : line)).TrimEnd();
            }
            var originalVerses = SplitVerses(originalLines);
            var translatedVerses = SplitVerses(translatedLines);
            if (originalVerses.Count != translatedVerses.Count) return originalLrc;
            var result = originalLines.Where(line => MetadataLine.IsMatch(line.Trim()) || CreditLine.IsMatch(line.Trim())).ToList();
            for (var i = 0; i < originalVerses.Count; i++)
            {
                if (i > 0) result.Add("");
                var verse = originalVerses[i];
                var translatedVerse = translatedVerses[i];
                if (verse.Count(IsText) == translatedVerse.Length)
                {
                    var index = 0;
                    result.AddRange(verse.Select(line => IsText(line) ? line + " / " + translatedVerse[index++] : line));
                }
                else
                {
                    // Keep both texts beside the same verse, preserving every line break.
                    result.AddRange(verse); result.Add(""); result.AddRange(translatedVerse);
                }
            }
            return string.Join("\n", result);
        }

        private static List<string[]> SplitVerses(IEnumerable<string> lines)
        {
            var verses = new List<string[]>();
            var current = new List<string>();
            foreach (var line in lines)
            {
                if (MetadataLine.IsMatch(line.Trim()) || CreditLine.IsMatch(line.Trim())) continue;
                if (!string.IsNullOrWhiteSpace(line)) current.Add(line.Trim());
                else if (current.Any(IsText)) { verses.Add(current.ToArray()); current.Clear(); }
            }
            if (current.Any(IsText)) verses.Add(current.ToArray());
            return verses;
        }

        private sealed class TimedDocument
        {
            internal readonly List<string> Headers = new List<string>();
            internal readonly List<TimedText> Entries = new List<TimedText>();
            // Malformed cues are not alignment candidates or original/translation continuations.
            internal readonly List<LooseText> LooseLines = new List<LooseText>();
            internal int Offset, InvalidTimes;
        }
        private sealed class LooseText { internal long? AfterTime; internal string Text; }
        private static void AppendLooseText(ILookup<long?, LooseText> looseLines, long? afterTime, List<string> lines)
        {
            foreach (var loose in looseLines[afterTime]) lines.Add(loose.Text);
        }
        private sealed class TimedText { internal long Time; internal string Text; }
        private static TimedText[] Cues(TimedDocument document) => document.Entries.OrderBy(c => c.Time).GroupBy(c => c.Time)
            .Select(g => new TimedText { Time = g.Key, Text = string.Join("\n", g.Select(c => c.Text).Distinct(StringComparer.Ordinal)) }).ToArray();

        private static TimedDocument ReadTimedDocument(string lrc)
        {
            var document = new TimedDocument();
            var previous = new List<TimedText>();
            long? lastTime = null;
            LooseText lastLoose = null;
            Action<string> isolate = line =>
            {
                var text = Regex.Replace(line, @"^(?:\[[^\]]*\])+", "");
                lastLoose = new LooseText { AfterTime = lastTime, Text = text };
                document.LooseLines.Add(lastLoose);
                document.InvalidTimes++; previous.Clear();
            };
            foreach (var rawLine in NormalizeLines(lrc).Split('\n'))
            {
                var trimmed = rawLine.TrimStart();
                var line = LeadingTimes.IsMatch(trimmed) || IsHeader(trimmed) || Regex.IsMatch(trimmed, @"^\[[0-9]+:") ? trimmed : rawLine;
                var match = LeadingTimes.Match(line);
                // A valid tag followed by a broken repeat tag does not establish an unambiguous cue.
                if (match.Success && Regex.IsMatch(line.Substring(match.Length), @"^\[[0-9]+:"))
                { isolate(line); continue; }
                if (match.Success)
                {
                    previous.Clear(); lastLoose = null;
                    foreach (Capture capture in match.Groups["time"].Captures)
                    {
                        var time = LeadingTimes.Match(capture.Value);
                        if (!int.TryParse(time.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)) continue;
                        var seconds = int.Parse(time.Groups["seconds"].Value, CultureInfo.InvariantCulture);
                        var fraction = time.Groups["fraction"].Value.PadRight(3, '0').Substring(0, 3);
                        var entry = new TimedText { Time = (long)minutes * 60000 + seconds * 1000 + int.Parse(fraction, CultureInfo.InvariantCulture),
                            Text = line.Substring(match.Length) };
                        document.Entries.Add(entry); previous.Add(entry);
                    }
                    if (previous.Count == 0) isolate(line);
                    else lastTime = previous.Min(c => c.Time);
                }
                else if (IsHeader(line))
                {
                    document.Headers.Add(CreditHeaderForDisplay(line));
                    var offset = Regex.Match(line, @"^\[offset:\s*([+-]?[0-9]+)\s*\]$", RegexOptions.IgnoreCase);
                    if (offset.Success && int.TryParse(offset.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) document.Offset = value;
                    previous.Clear(); lastLoose = null;
                }
                else if (Regex.IsMatch(line, @"^\[[0-9]+:"))
                {
                    isolate(line);
                }
                else if (previous.Count > 0)
                {
                    if (line.Length > 0) foreach (var entry in previous) entry.Text += "\n" + line;
                }
                else if (lastLoose != null && !string.IsNullOrWhiteSpace(line)) lastLoose.Text += "\n" + line;
                else if (!string.IsNullOrWhiteSpace(line)) document.Headers.Add(line);
            }
            return document;
        }
        private static string NormalizeLines(string text) => text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        private static string FormatTimestamp(long time) => "[" + (time / 60000).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (time / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + "." + (time % 1000).ToString("000", CultureInfo.InvariantCulture) + "]";
    }
}
