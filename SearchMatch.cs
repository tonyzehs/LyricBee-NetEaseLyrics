using F23.StringSimilarity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static class SearchMatch
    {
        private static readonly Regex ParenthesizedFeat = new Regex(
            @"\s*\((?:feat\.?|featuring|ft\.?)\s+(?<artists>[^()]+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex TrailingFeat = new Regex(
            @"\s+(?:feat\.?|featuring|ft\.?)\s+(?<artists>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ArtistSeparator = new Regex(@"\s*[;/、；]\s*");
        private static readonly Regex AcronymWithSlash = new Regex(@"^[A-Z0-9]{1,5}\s*/\s*[A-Z0-9]{1,5}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex EditionContainer = new Regex(@"\((?<label>[^)]*)\)|\[(?<label>[^]]*)\]|\s+[-–—]\s+(?<label>.+)$");
        private static readonly Regex Edition = new Regex(
            @"\b(live|remix|instrumental|karaoke|acoustic|extended|cover|radio\s+edit|tv(?:\s*(size|ver(?:sion)?))?)\b|现场|現場|伴奏|翻唱",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex TrailingEdition = new Regex(
            @"\s+(?<label>live|remix|instrumental|karaoke|acoustic|extended|cover|radio\s+edit|tv(?:\s*(?:size|ver(?:sion)?))?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RemasterNote = new Regex(
            @"^(?:[0-9]{4}\s+)?(?:digitally\s+)?re-?master(?:ed)?(?:\s+(?:in\s+)?[0-9]{4})?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex DurationNote = new Regex(@"^(?:mono|stereo|album\s+version)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PartNumber = new Regex(
            @"\b(?:part|pt\.?)\s*(?:[.:#-]\s*)?(?<number>[0-9]+|[ivxlcdm]+)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static long SearchAndMatch(string title, string artist, string album, long? duration,
            LookupSession session, bool allowTypos = true)
            => SearchCandidates(title, artist, album, duration, session, allowTypos).FirstOrDefault();

        internal static long[] SearchCandidates(string title, string artist, string album, long? duration,
            LookupSession session, bool allowTypos = true)
        {
            var split = SplitTitleArtist(title, artist);
            var request = new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos);
            if (request.Title.Length == 0) return new long[0];
            var songs = new List<SearchResultSong>();
            foreach (var query in BuildQueries(split.Item1, request.Artists, album))
            {
                if (!session.CanSearch) break;
                var found = NeteaseApi.Search(query, session).ToArray();
                songs.AddRange(found);
                // A code=200 payload can still contain unrelated songs. Try the other search interface.
                if (!found.Any(song => Evaluate(song, request) != null))
                    songs.AddRange(NeteaseApi.SearchLegacy(query, session));
                var best = Choose(songs, request);
                if (best.Length > 0 && best[0].Strong) return ReportSelection(best, session);
            }
            var final = Choose(songs, request);
            if (final.Length == 0)
                session.Report("Matching: no sufficiently supported, unambiguous candidate");
            return ReportSelection(final, session);
        }

        // Kept independent of transport so recorded search responses can be replayed in tests.
        internal static long SelectCandidate(IEnumerable<SearchResultSong> songs, string title, string artist,
            string album, long? duration, bool allowTypos = true)
        {
            var split = SplitTitleArtist(title, artist);
            return Choose(songs, new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos))
                .Select(m => m.Song.id).FirstOrDefault();
        }

        private static long[] ReportSelection(Match[] matches, LookupSession session)
        {
            if (matches.Length == 0) return new long[0];
            if (matches.Length > 1)
                session.Report("Matching: grouped " + matches.Length + " compatible listings; selected song ID " +
                    matches[0].Song.id + " by album similarity, then duration, then ID");
            if (matches[0].Notes.Length > 0)
                session.Report("Matching: treated recognized " + matches[0].Notes + " annotation as a note");
            // No extra searches or lyric comparisons are made to group catalogue entries.
            return matches.Take(3).Select(m => m.Song.id).ToArray();
        }

        private static IEnumerable<string> BuildQueries(string title, string[] artists, string album)
        {
            var queries = new List<string>();
            foreach (var artist in artists.Take(3)) queries.Add(title + " " + artist);
            if (artists.Length > 1) queries.Add(title + " " + string.Join(" ", artists.Take(3)));
            if (!string.IsNullOrWhiteSpace(album))
                queries.Add(title + " " + string.Join(" ", artists.Take(3)) + " " + NormalizeText(album));
            queries.Add(title);
            return queries.Select(q => Regex.Replace(q, @"\s+", " ").Trim()).Where(q => q.Length > 0).Distinct(StringComparer.Ordinal);
        }

        private static Match[] Choose(IEnumerable<SearchResultSong> songs, TrackIdentity request)
        {
            var matches = songs.Where(s => s != null && s.id > 0).Select(s => Evaluate(s, request))
                .Where(m => m != null).GroupBy(m => m.Song.id).Select(g => g.OrderByDescending(m => m.Score).First())
                .OrderBy(m => m.Song.duration).ThenBy(m => m.Song.id).ToArray();
            var groups = new List<List<Match>>();
            foreach (var match in matches)
            {
                // Check every member: 0s/2s/4s differences must not form a bridged group.
                var group = groups.FirstOrDefault(g => g.All(m => Equivalent(m, match)));
                if (group == null) groups.Add(new List<Match> { match });
                else group.Add(match);
            }
            var eligible = groups.AsEnumerable();
            if (groups.Any(g => g.Any(m => m.ExactIdentityAndAlbum)))
                eligible = eligible.Where(g => g.Any(m => m.ExactIdentityAndAlbum));
            var ranked = eligible.OrderByDescending(g => g.Max(m => m.Score))
                .ThenBy(g => g.Min(m => m.Song.id)).ToArray();
            if (ranked.Length == 0 || (ranked.Length > 1 &&
                ranked[0].Max(m => m.Score) - ranked[1].Max(m => m.Score) < 3)) return new Match[0];
            return ranked[0].OrderByDescending(m => m.AlbumScore).ThenBy(m => m.DurationDifference)
                .ThenBy(m => m.Song.id).ToArray();
        }

        private static bool Equivalent(Match a, Match b) => a.ExactTitleAndArtist && b.ExactTitleAndArtist &&
            a.Identity == b.Identity && a.Song.duration > 0 && b.Song.duration > 0 &&
            Math.Abs((double)a.Song.duration - b.Song.duration) <= 2000;

        private static Match Evaluate(SearchResultSong song, TrackIdentity request)
        {
            if (song == null || song.id <= 0 || string.IsNullOrWhiteSpace(song.name) || song.name.Length > 512) return null;
            var creditedArtists = (song.artists ?? new SearchResultArtist[0])
                .Where(a => !string.IsNullOrWhiteSpace(a?.name)).Select(a => a.name).ToArray();
            var candidate = SplitTitleArtist(song.name, string.Join(";", creditedArtists));
            var durationClose = request.Duration.HasValue && request.Duration > 0 && song.duration > 0 &&
                Math.Abs((double)request.Duration.Value - song.duration) <= 2000;
            var expected = PrepareTitle(request.RawTitle, durationClose, out var requestNotes);
            var actual = PrepareTitle(candidate.Item1, durationClose, out var candidateNotes);
            var title = Canonical(actual);
            var expectedTitle = Canonical(expected);
            if (expectedTitle.Length == 0 || title.Length == 0 ||
                Editions(request.RawTitle) != Editions(candidate.Item1) || Numbers(expected) != Numbers(actual)) return null;
            var titleScore = Similarity(expectedTitle, title);
            var titleExact = expectedTitle == title;
            // Require the whole title, allowing at most two edits on sufficiently long titles.
            if (!titleExact && (!request.AllowTypos || titleScore < .9 ||
                Math.Min(expectedTitle.Length, title.Length) < 6 || new Levenshtein().Distance(expectedTitle, title) > 2)) return null;

            var artists = candidate.Item2.Select(Canonical).Where(a => a.Length > 0).ToArray();
            var artistScore = ArtistSimilarity(request.Artists.Take(1).ToArray(), artists, request.AllowTypos);
            var album = Canonical(song.album?.name);
            var albumScore = Similarity(request.Album, album);
            // If no artist was supplied, an available album tag must support the match.
            if (request.Artists.Length == 0 && request.Album.Length > 0 && albumScore < .9) return null;

            double durationScore = 0;
            var durationDifference = double.MaxValue;
            if (request.Duration.HasValue && request.Duration > 0 && song.duration > 0)
            {
                var seconds = Math.Abs(request.Duration.Value / 1000.0 - song.duration / 1000.0);
                durationDifference = seconds;
                if (seconds > Math.Max(20, request.Duration.Value / 10000.0)) return null;
                durationClose = seconds <= 2;
                // Duration supplies at most five points, never enough to outweigh song identity.
                durationScore = Math.Max(0, 1 - Math.Max(0, seconds - 2) / 18);
            }
            if (request.Artists.Length > 0 && artistScore < .92)
            {
                // A secondary composer/guest credit alone must not identify a different recording.
                // Accept service credit-order differences only with corroborating album and duration.
                artistScore = ArtistSimilarity(request.Artists, artists, request.AllowTypos);
                if (artistScore < .92 || !titleExact || albumScore != 1 || !durationClose) return null;
            }
            return new Match
            {
                Song = song,
                AlbumScore = albumScore,
                DurationDifference = durationDifference,
                ExactTitleAndArtist = titleExact && artistScore == 1 && artists.Length > 0,
                Identity = title + "\0" + string.Join(";", artists.Distinct().OrderBy(a => a, StringComparer.Ordinal)) +
                    "\0" + Editions(candidate.Item1),
                Notes = string.Join("/", new[] { requestNotes, candidateNotes }.Where(n => n.Length > 0).Distinct()),
                Score = 60 * titleScore + 30 * artistScore + 5 * albumScore + 5 * durationScore,
                ExactIdentityAndAlbum = titleExact && artistScore == 1 && albumScore == 1,
                Strong = titleExact && artistScore == 1 && albumScore == 1 &&
                    (!request.Duration.HasValue || durationClose)
            };
        }

        private static string PrepareTitle(string title, bool durationClose, out string notes)
        {
            var stripped = new List<string>();
            var value = EditionContainer.Replace(NormalizeText(title), match =>
            {
                var label = match.Groups["label"].Value.Trim();
                if (RemasterNote.IsMatch(label)) { stripped.Add("remaster"); return ""; }
                if (durationClose && DurationNote.IsMatch(label)) { stripped.Add(label.ToLowerInvariant()); return ""; }
                return match.Value;
            });
            notes = string.Join("/", stripped.Distinct());
            return PartNumber.Replace(value, match =>
            {
                var number = match.Groups["number"].Value;
                if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var decimalNumber))
                    return "part " + decimalNumber.ToString(CultureInfo.InvariantCulture);
                // Conventional uppercase numerals avoid treating a word such as "mix" as 1009.
                if (number != number.ToUpperInvariant()) return match.Value;
                var roman = RomanNumber(number);
                return roman > 0 ? "part " + roman.ToString(CultureInfo.InvariantCulture) : match.Value;
            });
        }

        private static int RomanNumber(string text)
        {
            text = text.ToUpperInvariant();
            if (!Regex.IsMatch(text, @"^(?=.+$)M{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$")) return 0;
            var total = 0;
            var previous = 0;
            foreach (var c in text.Reverse())
            {
                var value = new[] { 1, 5, 10, 50, 100, 500, 1000 }["IVXLCDM".IndexOf(c)];
                total += value < previous ? -value : value;
                previous = value;
            }
            return total;
        }

        private static string Numbers(string title) => string.Join("|", Regex.Matches(title, @"\p{Nd}+|\b[IVXLCDM]+\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Cast<System.Text.RegularExpressions.Match>().Select(m => char.IsDigit(m.Value[0]) ? "D:" + m.Value :
                RomanNumber(m.Value) > 0 ? "R:" + m.Value.ToUpperInvariant() : "").Where(n => n.Length > 0));

        private static double ArtistSimilarity(string[] expected, string[] actual, bool allowTypos)
        {
            if (expected.Length == 0 || actual.Length == 0) return 0;
            var names = actual.Concat(new[] { string.Concat(actual), string.Concat(actual.Reverse()) }).ToArray();
            double best = 0;
            foreach (var raw in expected)
            {
                var name = Canonical(raw);
                foreach (var candidate in names)
                {
                    if (name == candidate) return 1;
                    if (!allowTypos || Math.Min(name.Length, candidate.Length) < 12) continue;
                    var similarity = Similarity(name, candidate);
                    if (similarity >= .92 && new Levenshtein().Distance(name, candidate) <= 2)
                        best = Math.Max(best, similarity);
                }
            }
            return best;
        }

        private static double Similarity(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0 || a.Length > 1024 || b.Length > 1024) return 0;
            return 1 - new Levenshtein().Distance(a, b) / (double)Math.Max(a.Length, b.Length);
        }

        private static string Editions(string title)
        {
            var labels = EditionContainer.Matches(title).Cast<System.Text.RegularExpressions.Match>()
                .SelectMany(m => Edition.Matches(m.Groups["label"].Value).Cast<System.Text.RegularExpressions.Match>())
                .Select(m => Canonical(m.Value)).ToList();
            var trailing = TrailingEdition.Match(title);
            if (trailing.Success) labels.Add(Canonical(trailing.Groups["label"].Value));
            return string.Join("|", labels.Distinct().OrderBy(s => s, StringComparer.Ordinal));
        }

        private static string NormalizeText(string value)
        {
            return (value ?? "").Normalize(NormalizationForm.FormKC).Replace('\u00A0', ' ').Trim();
        }

        internal static string Canonical(string value)
        {
            var text = NormalizeText(value).ToLowerInvariant();
            var result = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(text, i);
                var width = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                if (category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter ||
                    category == UnicodeCategory.TitlecaseLetter || category == UnicodeCategory.ModifierLetter ||
                    category == UnicodeCategory.OtherLetter || category == UnicodeCategory.DecimalDigitNumber)
                    result.Append(text, i, width);
                i += width - 1;
            }
            return result.ToString();
        }

        public static (string, IEnumerable<string>) SplitTitleArtist(string title, string artist)
        {
            var artistSplit = ExtractFeat(NormalizeText(artist));
            var titleSplit = ExtractFeat(NormalizeText(title));
            var artists = SplitArtists(artistSplit.Item1).Concat(artistSplit.Item2).Concat(titleSplit.Item2)
                .Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return (titleSplit.Item1, artists);
        }

        private static IEnumerable<string> SplitArtists(string value)
        {
            // Commas, ampersands and middle dots often belong to a real artist's name.
            if (AcronymWithSlash.IsMatch(value)) return new[] { value };
            return ArtistSeparator.Split(value).Select(a => a.Trim()).Where(a => a.Length > 0);
        }

        private static (string, IEnumerable<string>) ExtractFeat(string value)
        {
            var match = ParenthesizedFeat.Match(value);
            if (!match.Success) match = TrailingFeat.Match(value);
            if (!match.Success) return (value, Enumerable.Empty<string>());
            return (value.Remove(match.Index, match.Length).Trim(), SplitArtists(match.Groups["artists"].Value));
        }

        private sealed class TrackIdentity
        {
            internal readonly string RawTitle, Title, Album;
            internal readonly string[] Artists;
            internal readonly long? Duration;
            internal readonly bool AllowTypos;
            internal TrackIdentity(string title, string[] artists, string album, long? duration, bool allowTypos)
            {
                RawTitle = title; Title = Canonical(title); Artists = artists;
                Album = Canonical(album); Duration = duration; AllowTypos = allowTypos;
            }
        }

        private sealed class Match
        {
            internal SearchResultSong Song;
            internal double Score;
            internal double AlbumScore, DurationDifference;
            internal string Identity, Notes;
            internal bool ExactTitleAndArtist;
            internal bool Strong;
            internal bool ExactIdentityAndAlbum;
        }
    }
}
