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

        public static long SearchAndMatch(string title, string artist, string album, long? duration,
            LookupSession session, bool allowTypos = true)
        {
            var split = SplitTitleArtist(title, artist);
            var request = new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos);
            if (request.Title.Length == 0) return 0;
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
                if (best != null && best.Strong) return best.Song.id;
            }
            var final = Choose(songs, request);
            if (final == null)
                session.Report("Matching: no sufficiently supported, unambiguous candidate");
            return final?.Song.id ?? 0;
        }

        // Kept independent of transport so recorded search responses can be replayed in tests.
        internal static long SelectCandidate(IEnumerable<SearchResultSong> songs, string title, string artist,
            string album, long? duration, bool allowTypos = true)
        {
            var split = SplitTitleArtist(title, artist);
            return Choose(songs, new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos))?.Song.id ?? 0;
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

        private static Match Choose(IEnumerable<SearchResultSong> songs, TrackIdentity request)
        {
            var matches = songs.Where(s => s != null && s.id > 0).Select(s => Evaluate(s, request))
                .Where(m => m != null).GroupBy(m => m.Song.id).Select(g => g.OrderByDescending(m => m.Score).First())
                .OrderByDescending(m => m.Score).ToArray();
            if (matches.Length == 0) return null;
            var exactAlbum = matches.Where(m => m.ExactIdentityAndAlbum).ToArray();
            if (exactAlbum.Length > 0) matches = exactAlbum;
            // ID or server result order cannot establish which of two plausible recordings is correct.
            if (matches.Length > 1 && matches[0].Score - matches[1].Score < 3) return null;
            return matches[0];
        }

        private static Match Evaluate(SearchResultSong song, TrackIdentity request)
        {
            if (song == null || song.id <= 0 || string.IsNullOrWhiteSpace(song.name) || song.name.Length > 512) return null;
            var creditedArtists = (song.artists ?? new SearchResultArtist[0])
                .Where(a => !string.IsNullOrWhiteSpace(a?.name)).Select(a => a.name).ToArray();
            var candidate = SplitTitleArtist(song.name, string.Join(";", creditedArtists));
            var title = Canonical(candidate.Item1);
            if (request.Title.Length == 0 || title.Length == 0 || Editions(request.RawTitle) != Editions(candidate.Item1)) return null;
            var titleScore = Similarity(request.Title, title);
            var titleExact = request.Title == title;
            // Require the whole title, allowing at most two edits on sufficiently long titles.
            if (!titleExact && (!request.AllowTypos || titleScore < .9 ||
                Math.Min(request.Title.Length, title.Length) < 6 || new Levenshtein().Distance(request.Title, title) > 2)) return null;

            var artists = candidate.Item2.Select(Canonical).Where(a => a.Length > 0).ToArray();
            var artistScore = ArtistSimilarity(request.Artists.Take(1).ToArray(), artists, request.AllowTypos);
            var album = Canonical(song.album?.name);
            var albumScore = Similarity(request.Album, album);
            // If no artist was supplied, an available album tag must support the match.
            if (request.Artists.Length == 0 && request.Album.Length > 0 && albumScore < .9) return null;

            double durationScore = 0;
            var durationClose = false;
            if (request.Duration.HasValue && request.Duration > 0 && song.duration > 0)
            {
                var seconds = Math.Abs(request.Duration.Value / 1000.0 - song.duration / 1000.0);
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
                Score = 60 * titleScore + 30 * artistScore + 5 * albumScore + 5 * durationScore,
                ExactIdentityAndAlbum = titleExact && artistScore == 1 && albumScore == 1,
                Strong = titleExact && artistScore == 1 && albumScore == 1 &&
                    (!request.Duration.HasValue || durationClose)
            };
        }

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
            internal bool Strong;
            internal bool ExactIdentityAndAlbum;
        }
    }
}
