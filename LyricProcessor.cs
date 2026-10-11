using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static class LyricProcessor
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
        private static bool IsHeader(string line) => MetadataLine.IsMatch(line.Trim()) || SectionLine.IsMatch(line.Trim()) || CreditLine.IsMatch(line.Trim());
        public static string InjectTranslation(string original, string translation) => MergeTranslation(original, translation, false);
        public static string InjectTranslationTwoLines(string original, string translation) => MergeTranslation(original, translation, true);

        internal static string MergeTranslation(string originalLrc, string translationLrc, bool twoLines, Action<string> trace = null)
        {
            if (!HasLyricText(translationLrc) || string.IsNullOrWhiteSpace(originalLrc)) return originalLrc;
            var original = ReadTimedDocument(originalLrc);
            var translation = ReadTimedDocument(translationLrc);
            if (original.Entries.Count == 0) return FormatUntimedTranslation(originalLrc, translationLrc);
            if (translation.Entries.Count == 0) return originalLrc;
            var originals = Cues(original);
            var translations = Cues(translation).Where(c => HasLyricText(c.Text)).ToArray();
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
            var lines = new List<string>(original.Headers);
            for (var index = 0; index < originals.Length; index++)
            {
                var cue = originals[index];
                var content = cue.Text;
                if (HasLyricText(content) && alignment.Pairs.TryGetValue(index, out var pair))
                    content += (twoLines ? "\n" : "/") + translations[pair.Translation].Text;
                // A single timestamp owns the original and its continuation in MusicBee.
                lines.Add(FormatTimestamp(cue.Time) + content);
            }
            return string.Join("\n", lines);
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
            var originalLines = NormalizeLines(originalLrc).Split('\n');
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
            internal int Offset, InvalidTimes;
        }
        private sealed class TimedText { internal long Time; internal string Text; }
        private static TimedText[] Cues(TimedDocument document) => document.Entries.OrderBy(c => c.Time).GroupBy(c => c.Time)
            .Select(g => new TimedText { Time = g.Key, Text = string.Join("\n", g.Select(c => c.Text).Distinct(StringComparer.Ordinal)) }).ToArray();

        private static TimedDocument ReadTimedDocument(string lrc)
        {
            var document = new TimedDocument();
            var previous = new List<TimedText>();
            foreach (var rawLine in NormalizeLines(lrc).Split('\n'))
            {
                var trimmed = rawLine.TrimStart();
                var line = LeadingTimes.IsMatch(trimmed) || IsHeader(trimmed) || Regex.IsMatch(trimmed, @"^\[[0-9]+:") ? trimmed : rawLine;
                var match = LeadingTimes.Match(line);
                if (match.Success)
                {
                    previous.Clear();
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
                    if (previous.Count == 0) { document.Headers.Add(line); document.InvalidTimes++; }
                }
                else if (IsHeader(line))
                {
                    document.Headers.Add(line);
                    var offset = Regex.Match(line, @"^\[offset:\s*([+-]?[0-9]+)\s*\]$", RegexOptions.IgnoreCase);
                    if (offset.Success && int.TryParse(offset.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) document.Offset = value;
                    previous.Clear();
                }
                else if (Regex.IsMatch(line, @"^\[[0-9]+:"))
                {
                    var text = Regex.Replace(line, @"^(?:\[[^\]]*\])+", "");
                    if (!string.IsNullOrWhiteSpace(text)) document.Headers.Add(text);
                    document.InvalidTimes++; previous.Clear();
                }
                else if (previous.Count > 0)
                {
                    if (line.Length > 0) foreach (var entry in previous) entry.Text += "\n" + line;
                }
                else if (!string.IsNullOrWhiteSpace(line)) document.Headers.Add(line);
            }
            return document;
        }
        private static string NormalizeLines(string text) => text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        private static string FormatTimestamp(long time) => "[" + (time / 60000).ToString("00", CultureInfo.InvariantCulture) + ":" +
            (time / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + "." + (time % 1000).ToString("000", CultureInfo.InvariantCulture) + "]";
    }
}
