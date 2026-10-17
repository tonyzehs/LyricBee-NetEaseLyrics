using F23.StringSimilarity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static partial class SearchMatch
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
            @"^(?:[0-9]{4}\s*(?:[-–—]\s*)?)?(?:(?:digital|digitally)\s+)?re-?master(?:ed)?(?:\s+version)?(?:\s+(?:in\s+)?[0-9]{4})?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex DurationNote = new Regex(@"^(?:mono|stereo|album\s+version)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PartNumber = new Regex(
            @"\b(?:part|pt\.?)\s*(?:[.:#-]\s*)?(?<number>[0-9]+|[ivxlcdm]+)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SoundtrackSuffix = new Regex(
            @"(?:\s*\((?<label>from\s+[^()\[\]\r\n]+)\)|\s*\[(?<label>from\s+[^()\[\]\r\n]+)\]|\s+[-–—]\s+(?<label>from\s+[^()\[\]\r\n]+))\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SourceMedium = new Regex(
            @"^(?:the\s+)?(?:(?:original\s+)?soundtrack\s+(?:of|to)\s+|(?:(?:animated|television|tv)\s+series|series|film|movie|motion\s+picture|video\s+game|game|anime|musical)\s+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SoundtrackAlbum = new Regex(
            @"\b(?:soundtrack|ost|original\s+score)\b|原声|原聲",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static long SearchAndMatch(string title, string artist, string album, long? duration,
            LookupSession session, bool allowTypos = true)
            => SearchCandidates(title, artist, album, duration, session, allowTypos).FirstOrDefault();

        internal static long[] SearchCandidates(string title, string artist, string album, long? duration,
            LookupSession session, bool allowTypos = true, string trackNumber = null)
        {
            var split = SplitTitleArtist(title, artist);
            var request = new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos);
            var alternate = TrackPrefixIdentity(title, artist, album, duration, allowTypos, trackNumber);
            if (alternate != null) alternate.Facts = request.Facts;
            if (request.Title.Length == 0) return new long[0];
            var songs = new List<SearchResultSong>();
            var aliases = new Dictionary<long, string[]>();
            var queries = BuildQueries(split.Item1, request.Artists, album).ToArray();
            foreach (var query in queries)
            {
                if (!session.CanSearch) break;
                var found = NeteaseApi.Search(query, session).ToArray();
                songs.AddRange(found);
                // A code=200 payload can still contain unrelated songs. Try the other search interface.
                if (!found.Any(song => Evaluate(song, request, aliases) != null))
                    songs.AddRange(NeteaseApi.SearchLegacy(query, session));
                var best = Choose(songs, request, out var ambiguous, aliases: aliases);
                if (ambiguous) return ReportAmbiguity(session, songs.Count);
                if (best.Length == 0)
                {
                    ResolveArtistAliases(songs, request, session, aliases);
                    best = Choose(songs, request, out ambiguous, aliases: aliases);
                    if (ambiguous) return ReportAmbiguity(session, songs.Count);
                }
                if (best.Length > 0 && best[0].Strong) return ReportSelection(best, session);
            }
            // Try one normalized query only if the ordinary searches found no eligible identity.
            if (alternate != null && session.CanSearch && !songs.Any(s => Evaluate(s, request, aliases) != null || Evaluate(s, alternate) != null))
            {
                var query = BuildQueries(alternate.RawTitle, alternate.Artists, null).FirstOrDefault();
                if (query != null)
                {
                    var found = NeteaseApi.Search(query, session).ToArray(); songs.AddRange(found);
                    if (!found.Any(s => Evaluate(s, alternate) != null)) songs.AddRange(NeteaseApi.SearchLegacy(query, session));
                }
            }
            var final = Choose(songs, request, out var finalAmbiguous, alternate, aliases, allowPostSearch: true);
            if (finalAmbiguous) return ReportAmbiguity(session, songs.Count);
            if (final.Length == 0)
            {
                session.Report("Matching: no sufficiently supported, unambiguous candidate");
                session.Diagnostic(DiagnosticKind.MatchingNoEligible, count: songs.Count);
            }
            return ReportSelection(final, session);
        }

        // Kept independent of transport so recorded search responses can be replayed in tests.
        internal static long SelectCandidate(IEnumerable<SearchResultSong> songs, string title, string artist,
            string album, long? duration, bool allowTypos = true, string trackNumber = null)
        {
            var split = SplitTitleArtist(title, artist);
            var request = new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos);
            var alternate = TrackPrefixIdentity(title, artist, album, duration, allowTypos, trackNumber);
            if (alternate != null) alternate.Facts = request.Facts;
            return Choose(songs, request, out _, alternate, allowPostSearch: true).Select(m => m.Song.id).FirstOrDefault();
        }

        private static long[] ReportAmbiguity(LookupSession session, int count)
        {
            session.Diagnostic(DiagnosticKind.MatchingAmbiguous, count: count);
            session.Report("Matching: stopped because the matches disagree");
            return new long[0];
        }

        private static long[] ReportSelection(Match[] matches, LookupSession session)
        {
            if (matches.Length == 0) return new long[0];
            if (matches[0].Tier == MatchingTier.Ordinary)
            {
                session.Diagnostic(DiagnosticKind.OrdinaryMatch);
                session.Report("Matching: matched title and artist");
            }
            if (matches[0].Tier == MatchingTier.ShowCreditedSoundtrack)
            {
                session.Diagnostic(DiagnosticKind.ShowCreditedSoundtrackMatch);
                session.Report("Matching: matched a soundtrack credited to the show");
            }
            var albumPeers = matches.Where(m => m.Notes == "same-album soundtrack").ToArray();
            if (albumPeers.Length > 0)
            {
                session.Diagnostic(DiagnosticKind.SoundtrackAlbumPeer, songId: albumPeers[0].Song.id, count: albumPeers.Length);
                session.Report("Matching: included another listing from the same soundtrack album");
            }
            session.Diagnostic(DiagnosticKind.MatchingSelected, songId: matches[0].Song.id, count: matches.Length);
            if (matches.Length > 1) session.Diagnostic(DiagnosticKind.CompatibleGroup, count: matches.Length);
            if (matches[0].Notes.Contains("remaster")) session.Diagnostic(DiagnosticKind.RemasterNote);
            if (matches[0].Notes.Contains("mono") || matches[0].Notes.Contains("stereo") || matches[0].Notes.Contains("album version")) session.Diagnostic(DiagnosticKind.VersionNote);
            if (matches[0].Notes.Contains("soundtrack")) session.Diagnostic(DiagnosticKind.SoundtrackNote);
            if (matches[0].LocalizedSoundtrack)
            {
                session.Diagnostic(DiagnosticKind.LocalizedSoundtrackMatch);
                session.Report("Matching: matched a soundtrack with a different album language");
            }
            if (matches[0].Tier == MatchingTier.TrackPrefix)
            {
                session.Diagnostic(DiagnosticKind.TrackPrefixNote);
                session.Report("Matching: matched after removing the tagged track number");
            }
            if (matches[0].Notes.Contains("artist alias"))
            {
                session.Diagnostic(DiagnosticKind.ArtistAliasMatch);
                session.Report("Matching: matched through a verified NetEase artist alias");
            }
            if (matches.Length > 1)
                session.Report("Matching: grouped " + matches.Length + " compatible listings; selected song ID " +
                    matches[0].Song.id + " by album similarity, then duration, then ID");
            var annotationNotes = matches[0].Notes.Split('/').Where(n => n.Length > 0 && n != "verified artist alias" && n != "same-album soundtrack").ToArray();
            if (annotationNotes.Length > 0)
                session.Report("Matching: treated recognized " + string.Join("/", annotationNotes) + " annotation as a note");
            // No extra searches or lyric comparisons are made to group catalogue entries.
            return matches.Take(3).Select(m => m.Song.id).ToArray();
        }

        private static void ResolveArtistAliases(IEnumerable<SearchResultSong> songs, TrackIdentity request,
            LookupSession session, Dictionary<long, string[]> aliases)
        {
            // Only the primary credited artist can establish an alias match. Different singers
            // sharing a composer/guest credit must not borrow that person's alias.
            var ids = songs.Where(s => Evaluate(s, request, ignoreArtist: true) != null)
                .Select(s => s.artists?.FirstOrDefault()?.id ?? 0).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length > LookupSession.MaxArtistRequests) return;
            foreach (var id in ids)
            {
                if (aliases.ContainsKey(id)) continue;
                var names = NeteaseApi.RequestArtistAliases(id, session);
                if (names != null) aliases[id] = names;
            }
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

        private static Match[] Choose(IEnumerable<SearchResultSong> songs, TrackIdentity request, out bool ambiguous, TrackIdentity alternate = null,
            IReadOnlyDictionary<long, string[]> aliases = null, bool allowPostSearch = false)
        {
            ambiguous = false;
            var candidates = songs.Where(s => s != null && s.id > 0).ToArray();
            var supported = new Match[0];
            // An eligible but ambiguous tier is ranked below; it never falls through.
            foreach (var tier in MatchingOrder)
            {
                if (tier == MatchingTier.ArtistAlias && aliases == null) continue;
                if (tier == MatchingTier.TrackPrefix && (!allowPostSearch || alternate == null)) continue;
                if (tier >= MatchingTier.TranslatedSoundtrack && (!allowPostSearch || request.PrefixIdentity)) continue;
                supported = candidates.Select(song => EvaluateTier(song, request, alternate, aliases, tier))
                    .Where(match => match != null).ToArray();
                if (supported.Length == 0) continue;
                if (tier == MatchingTier.ShowCreditedSoundtrack)
                    supported = IncludeSoundtrackAlbumPeers(candidates, request, supported);
                if (tier >= MatchingTier.TranslatedSoundtrack &&
                    (supported.Select(m => m.SoundtrackSource).Distinct(StringComparer.Ordinal).Count() > 1 ||
                    (tier == MatchingTier.ShowCreditedSoundtrack && supported.Any(a => supported.Any(b => !Equivalent(a, b))))))
                { ambiguous = true; return new Match[0]; }
                break;
            }
            var matches = supported
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
                ranked[0].Max(m => m.Score) - ranked[1].Max(m => m.Score) < 3))
            { ambiguous = ranked.Length > 0; return new Match[0]; }
            return ranked[0].OrderByDescending(m => m.AlbumScore).ThenBy(m => m.DurationDifference)
                .ThenBy(m => m.Song.id).ToArray();
        }

        private static bool Equivalent(Match a, Match b) => a.ExactTitleAndArtist && b.ExactTitleAndArtist &&
            a.Identity == b.Identity && a.Song.duration > 0 && b.Song.duration > 0 &&
            (a.SoundtrackSource == null || b.SoundtrackSource == null || a.SoundtrackSource == b.SoundtrackSource) &&
            Math.Abs((double)a.Song.duration - b.Song.duration) <= 2000;

        private static Match Evaluate(SearchResultSong song, TrackIdentity request,
            IReadOnlyDictionary<long, string[]> aliases = null, bool ignoreArtist = false, bool allowLocalizedSoundtrack = false)
        {
            if (song == null || song.id <= 0 || string.IsNullOrWhiteSpace(song.name) || song.name.Length > 512) return null;
            var facts = Facts(song, request);
            var durationClose = request.Duration.HasValue && request.Duration > 0 && song.duration > 0 &&
                Math.Abs((double)request.Duration.Value - song.duration) <= 2000;
            var artists = facts.Artists;
            var album = facts.Album;
            if (request.PrefixIdentity && (!durationClose || request.Artists.Length == 0 ||
                !request.Artists.Select(Canonical).Distinct().OrderBy(a => a, StringComparer.Ordinal)
                    .SequenceEqual(artists.Distinct().OrderBy(a => a, StringComparer.Ordinal)) ||
                (request.Album.Length > 0 && request.Album != album))) return null;
            var expectedRaw = request.RawTitle;
            var actualRaw = facts.Title;
            string soundtrackSource = null;
            var conflictingSource = false;
            var localizedSoundtrack = false;
            var localizedAlias = false;
            var fullArtistsAgree = request.Artists.Length > 0 && request.Artists.Select(Canonical).Distinct().OrderBy(a => a, StringComparer.Ordinal)
                .SequenceEqual(artists.Distinct().OrderBy(a => a, StringComparer.Ordinal));
            var soundtrackNote = durationClose && request.Album.Length > 0 && album.Length > 0 &&
                fullArtistsAgree &&
                RemoveSoundtrackAttribution(ref expectedRaw, ref actualRaw, request.Album, request.RawAlbum,
                    album, song.album?.name, out soundtrackSource, out conflictingSource);
            if (conflictingSource) return null;
            if (!soundtrackNote && allowLocalizedSoundtrack && !ignoreArtist && !request.PrefixIdentity &&
                durationClose && fullArtistsAgree && TryLocalizedSoundtrack(request, song.album,
                    ref actualRaw, out soundtrackSource, out localizedAlias))
                soundtrackNote = localizedSoundtrack = true;
            // Without corroboration, attributed titles require their complete text to agree;
            // spelling allowance must not substitute a different named work.
            if (!soundtrackNote && (SoundtrackSuffix.IsMatch(expectedRaw) || SoundtrackSuffix.IsMatch(actualRaw)) &&
                Canonical(expectedRaw) != Canonical(actualRaw)) return null;
            var expected = PrepareTitle(expectedRaw, durationClose, out var requestNotes);
            var actual = PrepareTitle(actualRaw, durationClose, out var candidateNotes);
            var title = Canonical(actual);
            var expectedTitle = Canonical(expected);
            var editions = Editions(actualRaw);
            if (expectedTitle.Length == 0 || title.Length == 0 ||
                Editions(expectedRaw) != editions ||
                Numbers(expectedTitle == title ? expected.ToUpperInvariant() : expected) != Numbers(expectedTitle == title ? actual.ToUpperInvariant() : actual) ||
                DifferentRomanToken(expected, actual)) return null;
            var titleScore = Similarity(expectedTitle, title);
            var titleExact = expectedTitle == title;
            if (soundtrackNote && !titleExact) return null;
            // Require the whole title, allowing at most two edits on sufficiently long titles.
            if (!titleExact && (!request.AllowTypos || titleScore < .9 ||
                Math.Min(expectedTitle.Length, title.Length) < 6 || new Levenshtein().Distance(expectedTitle, title) > 2)) return null;

            var artistScore = ArtistSimilarity(request.Artists.Take(1).ToArray(), artists, request.AllowTypos);
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
            var aliasUsed = false;
            if (ignoreArtist && (request.Artists.Length == 0 || !titleExact || albumScore != 1 || !durationClose)) return null;
            if (!ignoreArtist && request.Artists.Length > 0 && artistScore < .92)
            {
                var primary = song.artists?.FirstOrDefault();
                if (titleExact && albumScore == 1 && durationClose && primary != null && primary.id > 0 &&
                    !string.IsNullOrWhiteSpace(primary.name) && aliases != null && aliases.TryGetValue(primary.id, out var names) &&
                    names.Any(n => Canonical(n) == Canonical(request.Artists[0])) &&
                    request.Artists.Skip(1).All(n => ArtistSimilarity(new[] { n }, artists, false) == 1))
                {
                    artistScore = 1; aliasUsed = true;
                }
                else
                {
                    // A secondary composer/guest credit alone must not identify a different recording.
                    // Accept service credit-order differences only with corroborating album and duration.
                    artistScore = ArtistSimilarity(request.Artists, artists, request.AllowTypos);
                    if (artistScore < .92 || !titleExact || albumScore != 1 || !durationClose) return null;
                }
            }
            return new Match
            {
                Song = song,
                AlbumScore = albumScore,
                DurationDifference = durationDifference,
                SoundtrackSource = soundtrackSource,
                LocalizedSoundtrack = localizedSoundtrack,
                ExactTitleAndArtist = titleExact && artistScore == 1 && artists.Length > 0,
                Identity = title + "\0" + string.Join(";", artists.Distinct().OrderBy(a => a, StringComparer.Ordinal)) +
                    "\0" + editions + (aliasUsed ? "\0alias-artist-id:" + song.artists[0].id.ToString(CultureInfo.InvariantCulture) : ""),
                Notes = string.Join("/", new[] { requestNotes, candidateNotes,
                    localizedSoundtrack ? (localizedAlias ? "localized soundtrack album alias" : "localized soundtrack script fallback") :
                        soundtrackNote ? "soundtrack attribution" : "", aliasUsed ? "verified artist alias" : "" }.Where(n => n.Length > 0).Distinct()),
                Score = 60 * titleScore + 30 * artistScore + 5 * albumScore + 5 * durationScore,
                ExactIdentityAndAlbum = titleExact && artistScore == 1 && albumScore == 1,
                Strong = !localizedSoundtrack && titleExact && artistScore == 1 && albumScore == 1 &&
                    (!request.Duration.HasValue || durationClose)
            };
        }

        private static bool TryLocalizedSoundtrack(TrackIdentity request, SearchResultAlbum album,
            ref string actual, out string source, out bool verifiedAlbumAlias)
        {
            source = null; verifiedAlbumAlias = false;
            if (request.Album.Length == 0 || string.IsNullOrWhiteSpace(album?.name) ||
                SoundtrackSuffix.IsMatch(NormalizeText(request.RawTitle))) return false;
            var match = SoundtrackSuffix.Match(NormalizeText(actual));
            if (!match.Success || !SoundtrackAlbum.IsMatch(album.name)) return false;
            if (Regex.IsMatch(request.RawAlbum + " " + album.name,
                @"\b(?:live|remix|demo|piano|karaoke|instrumental|sped|slowed|radio\s+edit|tv\s+size)\b|现场|現場|伴奏|翻唱",
                RegexOptions.IgnoreCase)) return false;
            var label = Regex.Replace(match.Groups["label"].Value, @"^from\s+", "", RegexOptions.IgnoreCase).Trim();
            // Require an explicit medium, not arbitrary '(from X)' text. For this weaker
            // corroboration, even version words inside the attribution remain disqualifying.
            if (!SourceMedium.IsMatch(label) || Edition.IsMatch(label) ||
                Regex.IsMatch(label, @"\b(?:demo|piano|sped|slowed|edit|version|original\s+mix)\b", RegexOptions.IgnoreCase)) return false;
            source = AttributionSource(match, Canonical(album.name), album.name);
            if (source == null) return false;
            verifiedAlbumAlias = new[] { album.alias, album.tns, album.transNames }
                .SelectMany(names => (names ?? new string[0]).Take(16)).Take(32)
                .Any(name => name != null && name.Length <= 512 && Canonical(name) == request.Album);
            if (!verifiedAlbumAlias && !DifferentScriptSoundtrack(request.RawAlbum, source)) return false;
            actual = NormalizeText(actual).Substring(0, match.Index).Trim();
            return true;
        }

        private static bool DifferentScriptSoundtrack(string album, string source)
        {
            // This is a bounded identity heuristic, not a translation of the local work name.
            // Same-script mismatches and mixed names containing another Latin work are retained.
            if (!Regex.IsMatch(source, @"^[a-z0-9]+$") || Regex.Matches(source, @"[a-z]").Count < 4 ||
                !Regex.IsMatch(album ?? "", @"原声|原聲|サウンドトラック|사운드트랙|\b(?:ost|soundtrack|original\s+score)\b", RegexOptions.IgnoreCase)) return false;
            var work = Regex.Replace(NormalizeText(album), @"\b(?:(?:original|motion|picture)\s+)*(?:soundtrack|ost|score)\b", "", RegexOptions.IgnoreCase);
            work = Regex.Replace(work, @"原[声聲](?:带|帶)?|サウンドトラック|オリジナル|사운드트랙|오리지널", "");
            return !Regex.IsMatch(work, @"[a-zA-Z]") &&
                Regex.Matches(work, @"[\u3040-\u30ff\u3400-\u9fff\uac00-\ud7af]").Count >= 2;
        }

        private static bool RemoveSoundtrackAttribution(ref string expected, ref string actual, string expectedAlbum, string expectedRawAlbum,
            string album, string rawAlbum,
            out string source, out bool conflicting)
        {
            source = null; conflicting = false;
            var expectedMatch = SoundtrackSuffix.Match(NormalizeText(expected));
            var actualMatch = SoundtrackSuffix.Match(NormalizeText(actual));
            if (!expectedMatch.Success && !actualMatch.Success) return false;
            var expectedSource = AttributionSource(expectedMatch, expectedAlbum, expectedRawAlbum);
            var actualSource = AttributionSource(actualMatch, album, rawAlbum);
            if ((expectedMatch.Success && expectedSource == null) || (actualMatch.Success && actualSource == null)) return false;
            if (expectedMatch.Success && actualMatch.Success && expectedSource != actualSource) { conflicting = true; return false; }
            source = actualSource ?? expectedSource;
            if (!expectedAlbum.Contains(source) || !album.Contains(source)) return false;
            // Only the terminal attribution is removable. Other annotations and title numbers survive.
            if (expectedMatch.Success) expected = NormalizeText(expected).Substring(0, expectedMatch.Index).Trim();
            if (actualMatch.Success) actual = NormalizeText(actual).Substring(0, actualMatch.Index).Trim();
            return true;
        }

        private static string AttributionSource(System.Text.RegularExpressions.Match match, string album, string rawAlbum)
        {
            if (!match.Success) return null;
            var label = Regex.Replace(match.Groups["label"].Value, @"^from\s+", "", RegexOptions.IgnoreCase).Trim();
            var medium = SourceMedium.Match(label);
            // Bare 'from X' also requires the album to identify itself as a soundtrack/score.
            if (!medium.Success && !SoundtrackAlbum.IsMatch(rawAlbum ?? "")) return null;
            if (medium.Success) label = label.Substring(medium.Length);
            var source = Canonical(label.Trim().Trim('"', '\'', '“', '”', '‘', '’'));
            return source.Length >= 4 && album.Contains(source) ? source : null;
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
                // An explicit part label provides number context even for lowercase numerals.
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

        private static string Numbers(string title)
        {
            title = Regex.Replace(title, @"(?<=\p{L})['’‘](?=\p{L})", "");
            // D.C. is an abbreviation, not two Roman numerals. Preserve II/V/etc. in titles.
            title = Regex.Replace(title, @"\b(?:[a-zA-Z]\.){2,}(?:[a-zA-Z]\b)?", m => m.Value.Replace(".", ""));
            return string.Join("|", Regex.Matches(title, @"\p{Nd}+|\b[IVXLCDM]+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Cast<System.Text.RegularExpressions.Match>().Select(m =>
                {
                    if (char.IsDigit(m.Value[0])) return "D:" + m.Value;
                    var numberedContext = Regex.IsMatch(title.Substring(0, m.Index), @"\b(?:part|pt\.?|vol\.?|no\.?)\s*$", RegexOptions.IgnoreCase);
                    if (m.Value != m.Value.ToUpperInvariant() && !numberedContext) return "";
                    if (m.Value.Equals("I", StringComparison.OrdinalIgnoreCase) &&
                        m.Index + m.Length < title.TrimEnd().Length &&
                        !numberedContext) return "";
                    return RomanNumber(m.Value) > 0 ? "R:" + m.Value.ToUpperInvariant() : "";
                }).Where(n => n.Length > 0));
        }

        internal static string RemoveTaggedTrackNumber(string title, string trackNumber)
        {
            if (!int.TryParse((trackNumber ?? "").Split('/')[0].Trim(), out var tagged) || tagged <= 0) return title;
            var prefix = Regex.Match(NormalizeText(title), @"^(?<number>[0-9]+)(?:(?<marked>\.(?![0-9])\s*|\s+-\s+)|\s+)(?<title>.+)$");
            if (prefix.Success && !prefix.Groups["marked"].Success &&
                !(prefix.Groups["number"].Value.Length > 1 && prefix.Groups["number"].Value.StartsWith("0"))) return title;
            return prefix.Success && int.TryParse(prefix.Groups["number"].Value, out var number) && number == tagged ?
                prefix.Groups["title"].Value.Trim() : title;
        }

        private static TrackIdentity TrackPrefixIdentity(string title, string artist, string album, long? duration, bool allowTypos, string trackNumber)
        {
            var clean = RemoveTaggedTrackNumber(title, trackNumber);
            if (clean == title) return null;
            var split = SplitTitleArtist(clean, artist);
            return new TrackIdentity(split.Item1, split.Item2.ToArray(), album, duration, allowTypos, true);
        }

        private static bool DifferentRomanToken(string expected, string actual)
        {
            var left = Regex.Matches(expected, @"[\p{L}\p{Nd}]+").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToArray();
            var right = Regex.Matches(actual, @"[\p{L}\p{Nd}]+").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToArray();
            if (left.Length != right.Length) return false;
            var different = Enumerable.Range(0, left.Length).Where(i => !left[i].Equals(right[i], StringComparison.OrdinalIgnoreCase)).ToArray();
            if (different.Length != 1) return false;
            var index = different[0];
            // Title-case words such as Mc are names; this guard covers the otherwise unprotected lowercase pair.
            return left[index] == left[index].ToLowerInvariant() && right[index] == right[index].ToLowerInvariant() &&
                left[index].Length >= 2 && right[index].Length >= 2 && RomanNumber(left[index]) > 0 &&
                RomanNumber(right[index]) > 0 && RomanNumber(left[index]) != RomanNumber(right[index]);
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
            internal readonly string RawTitle, Title, Album, RawAlbum;
            internal readonly string[] Artists;
            internal readonly long? Duration;
            internal readonly bool AllowTypos;
            internal readonly bool PrefixIdentity;
            internal Dictionary<SearchResultSong, CandidateFacts> Facts = new Dictionary<SearchResultSong, CandidateFacts>();
            internal TrackIdentity(string title, string[] artists, string album, long? duration, bool allowTypos, bool prefixIdentity = false)
            {
                RawTitle = title; Title = Canonical(title); Artists = artists;
                Album = Canonical(album); RawAlbum = album; Duration = duration; AllowTypos = allowTypos; PrefixIdentity = prefixIdentity;
            }
        }

        private sealed class Match
        {
            internal SearchResultSong Song;
            internal MatchingTier Tier;
            internal double Score;
            internal double AlbumScore, DurationDifference;
            internal string Identity, Notes;
            internal string SoundtrackSource;
            internal bool ExactTitleAndArtist;
            internal bool Strong;
            internal bool LocalizedSoundtrack;
            internal bool ExactIdentityAndAlbum;
        }
    }
}
