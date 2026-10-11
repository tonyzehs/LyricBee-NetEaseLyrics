using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class LyricProcessor
    {
        private static readonly Regex LyricLineRegex = new Regex(@"((\[.+?])+)(.*)", RegexOptions.Compiled);
        internal static bool IsInstrumentalPlaceholder(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var contents = text.TrimStart('\uFEFF').Split('\n')
                .Select(line => Regex.Replace(line, @"^(?:\s*\[[^\]]*\])*\s*", ""))
                .Select(line => Regex.Replace(line, @"[\s\p{P}]", ""))
                .Where(line => line.Length > 0).ToArray();
            return contents.Length > 0 && contents.All(line => line == "纯音乐请欣赏" || line == "純音樂請欣賞");
        }
        public static string InjectTranslation(string originalLrc, string translationLrc)
        {
            var originalEntries = ExpandEntries(Parse(originalLrc));
            var translationEntries = ExpandEntries(Parse(translationLrc));
            foreach (var originalEntry in originalEntries)
            {
                var translationEntry = translationEntries.FirstOrDefault(entry => entry.timeLabel == originalEntry.timeLabel);
                if (translationEntry != null)
                    originalEntry.content += "/" + translationEntry.content;
            }

            originalEntries.Sort();
            return string.Join("\n", originalEntries);
        }

        // MusicBee treats untimestamped text after a timestamp as a continuation of that cue.
        // Repeating the timestamp on the translation instead creates another active line.
        public static string InjectTranslationTwoLines(string originalLrc, string translationLrc)
        {
            if (string.IsNullOrWhiteSpace(originalLrc) || string.IsNullOrWhiteSpace(translationLrc))
                return originalLrc;
            var original = ReadTimedDocument(originalLrc);
            var translation = ReadTimedDocument(translationLrc);
            if (original.Entries.Count == 0)
            {
                return FormatUntimedTranslation(originalLrc, translationLrc);
            }
            if (translation.Entries.Count == 0) return originalLrc;

            var translatedCues = translation.Entries.GroupBy(entry => entry.Time - translation.Offset)
                .ToDictionary(group => group.Key, group => string.Join("\n", group.Select(entry => entry.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.Ordinal)));
            var lines = new List<string>(original.Headers);
            // Numeric ordering also handles legal unpadded minutes. Stable grouping keeps all
            // simultaneous originals/translations inside one host cue, rather than competing cues.
            foreach (var group in original.Entries.OrderBy(entry => entry.Time).GroupBy(entry => entry.Time))
            {
                var content = string.Join("\n", group.Select(entry => entry.Text).Distinct(StringComparer.Ordinal));
                if (!string.IsNullOrWhiteSpace(content) &&
                    translatedCues.TryGetValue(group.Key - original.Offset, out var translated) &&
                    !string.IsNullOrWhiteSpace(translated))
                    content += "\n" + translated;
                lines.Add(FormatTimestamp(group.Key) + content);
            }
            return string.Join("\n", lines);
        }

        private static string FormatUntimedTranslation(string originalLrc, string translationLrc)
        {
            var originalLines = NormalizeLines(originalLrc).Split('\n');
            var translatedLines = StripTimestamps(translationLrc).Split('\n')
                .Where(line => !MetadataLine.IsMatch(line)).ToArray();
            var originals = originalLines.Where(line => !MetadataLine.IsMatch(line) && !string.IsNullOrWhiteSpace(line)).ToArray();
            var translations = translatedLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            if (originals.Length == 0 || translations.Length == 0) return originalLrc;

            // Untimed pairing is positional, based on the provider's text order; it does
            // not establish semantic correspondence and never manufactures timestamps.
            if (originals.Length == translations.Length)
            {
                var index = 0;
                return string.Join("\n", originalLines.Select(line =>
                    MetadataLine.IsMatch(line) || string.IsNullOrWhiteSpace(line) ? line :
                    line.Trim() + " / " + translations[index++].Trim())).TrimEnd();
            }

            var originalVerses = SplitVerses(originalLines);
            var translatedVerses = SplitVerses(translatedLines);
            if (originalVerses.Count != translatedVerses.Count) return originalLrc;
            var result = originalLines.Where(line => MetadataLine.IsMatch(line)).ToList();
            for (var index = 0; index < originalVerses.Count; index++)
            {
                if (index > 0) result.Add("");
                var verse = originalVerses[index];
                var translatedVerse = translatedVerses[index];
                if (verse.Length == translatedVerse.Length)
                    result.AddRange(verse.Zip(translatedVerse, (text, translated) => text + " / " + translated));
                else
                    // A translation may split or combine lines. Keep the verse together
                    // instead of arbitrarily distributing its words across original lines.
                    result.Add(string.Join(" ", verse) + " / " + string.Join(" ", translatedVerse));
            }
            return string.Join("\n", result);
        }

        private static List<string[]> SplitVerses(IEnumerable<string> lines)
        {
            var verses = new List<string[]>();
            var current = new List<string>();
            foreach (var line in lines)
            {
                if (MetadataLine.IsMatch(line)) continue;
                if (!string.IsNullOrWhiteSpace(line)) current.Add(line.Trim());
                else if (current.Count > 0)
                {
                    verses.Add(current.ToArray());
                    current.Clear();
                }
            }
            if (current.Count > 0) verses.Add(current.ToArray());
            return verses;
        }

        private static readonly Regex LeadingTimes = new Regex(
            @"^(?<time>\[(?<minutes>[0-9]+):(?<seconds>[0-5][0-9])(?:[.:](?<fraction>[0-9]{1,3}))?\])+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex MetadataLine = new Regex(@"^\[[a-zA-Z][a-zA-Z0-9]*:.*\]$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private sealed class TimedDocument
        {
            internal readonly List<string> Headers = new List<string>();
            internal readonly List<TimedText> Entries = new List<TimedText>();
            internal int Offset;
        }

        private sealed class TimedText
        {
            internal long Time;
            internal string Text;
        }

        private static TimedDocument ReadTimedDocument(string lrc)
        {
            var document = new TimedDocument();
            var previous = new List<TimedText>();
            foreach (var line in NormalizeLines(lrc).Split('\n'))
            {
                var match = LeadingTimes.Match(line);
                if (match.Success)
                {
                    previous.Clear();
                    foreach (Capture capture in match.Groups["time"].Captures)
                    {
                        // Parse captures separately: optional fractions may be absent on some
                        // timestamps in a multi-timestamp line.
                        var time = LeadingTimes.Match(capture.Value);
                        if (!int.TryParse(time.Groups["minutes"].Value, NumberStyles.None,
                            CultureInfo.InvariantCulture, out var minutes)) continue;
                        var seconds = int.Parse(time.Groups["seconds"].Value, CultureInfo.InvariantCulture);
                        var fraction = time.Groups["fraction"].Value.PadRight(3, '0');
                        var milliseconds = int.Parse(fraction, CultureInfo.InvariantCulture);
                        var entry = new TimedText { Time = (long)minutes * 60000 + seconds * 1000 + milliseconds,
                            Text = line.Substring(match.Length) };
                        document.Entries.Add(entry);
                        previous.Add(entry);
                    }
                    if (previous.Count == 0) document.Headers.Add(line);
                }
                else if (MetadataLine.IsMatch(line))
                {
                    document.Headers.Add(line);
                    var offset = Regex.Match(line, @"^\[offset:\s*([+-]?[0-9]+)\s*\]$", RegexOptions.IgnoreCase);
                    if (offset.Success && int.TryParse(offset.Groups[1].Value, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out var value)) document.Offset = value;
                    previous.Clear();
                }
                else if (previous.Count > 0)
                {
                    // Preserve existing multiline cues, but avoid appending a trailing newline
                    // from the document terminator to a cue's separator.
                    if (line.Length > 0)
                        foreach (var entry in previous) entry.Text += "\n" + line;
                }
                else if (!string.IsNullOrWhiteSpace(line)) document.Headers.Add(line);
            }
            return document;
        }

        private static string NormalizeLines(string text) => text.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');

        private static string StripTimestamps(string text) => string.Join("\n", NormalizeLines(text).Split('\n')
            .Select(line => LeadingTimes.Replace(line, "")));

        private static string FormatTimestamp(long time) => "[" + (time / 60000).ToString("00", CultureInfo.InvariantCulture) +
            ":" + (time / 1000 % 60).ToString("00", CultureInfo.InvariantCulture) + "." +
            (time % 1000).ToString("000", CultureInfo.InvariantCulture) + "]";

        private static List<LyricEntry> Parse(string lrc)
        {
            var result = lrc.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => LyricLineRegex.Matches(line))
                .Where(matches => matches.Count >= 1)
                .Select(matches => matches[0])
                .Where(match => match.Groups.Count >= 3)
                .SelectMany(match => match.Groups[1].Captures.Cast<Capture>(),
                    (match, capture) => new LyricEntry(capture.Value, match.Groups[3].Value))
                .ToList();

            return result;
        }

        private static List<LyricEntry> ExpandEntries(List<LyricEntry> entries)
        {
            return entries.SelectMany(entry => entry.ExpandTimeLabel()).ToList();
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class LyricEntry : IComparable<LyricEntry>
    {
        private static readonly Regex LyricTimeRegex = new Regex(@"(\[[0-9.:]*])", RegexOptions.Compiled);

        public string timeLabel;
        public string content;

        public LyricEntry(string timeLabel, string content)
        {
            this.timeLabel = timeLabel;
            this.content = content;
        }

        public override string ToString()
        {
            return timeLabel + content;
        }

        public IEnumerable<LyricEntry> ExpandTimeLabel()
        {
            var matches = LyricTimeRegex.Matches(timeLabel);
            foreach (var match in matches.Cast<Match>())
                yield return new LyricEntry(match.Value, content);
        }

        public int CompareTo(LyricEntry other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return string.Compare(timeLabel, other.timeLabel, StringComparison.Ordinal);
        }
    }
}
