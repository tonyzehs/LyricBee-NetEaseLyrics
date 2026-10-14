using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static partial class SearchMatch
    {
        private enum MatchingTier { Ordinary, ArtistAlias, TrackPrefix, TranslatedSoundtrack, ShowCreditedSoundtrack }
        private static readonly MatchingTier[] MatchingOrder = {
            MatchingTier.Ordinary, MatchingTier.ArtistAlias, MatchingTier.TrackPrefix,
            MatchingTier.TranslatedSoundtrack, MatchingTier.ShowCreditedSoundtrack
        };
        private static readonly Regex SoundtrackVariant = new Regex(
            @"\b(?:live|remix|demo|piano|karaoke|instrumental|sped|slowed|radio\s+edit|tv\s+size)\b|现场|現場|伴奏|翻唱",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AttributionVariant = new Regex(
            @"\b(?:demo|piano|sped|slowed|edit|version|original\s+mix)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex FromPrefix = new Regex(@"^from\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LatinLetters = new Regex(@"[a-z]", RegexOptions.CultureInvariant);
        private static readonly Regex CjkLetters = new Regex(@"[\u3040-\u30ff\u3400-\u9fff\uac00-\ud7af]", RegexOptions.CultureInvariant);

        private sealed class CandidateFacts
        {
            internal string Title, Album, BaseTitle, Source, Attribution;
            internal string[] Artists;
            internal bool Soundtrack, ExplicitMedium, Excluded;
        }

        private static CandidateFacts Facts(SearchResultSong song, TrackIdentity request)
        {
            if (request.Facts.TryGetValue(song, out var facts)) return facts;
            var credits = (song.artists ?? new SearchResultArtist[0]).Where(a => !string.IsNullOrWhiteSpace(a?.name)).Select(a => a.name);
            var split = SplitTitleArtist(song.name, string.Join(";", credits));
            var title = NormalizeText(split.Item1);
            var attribution = SoundtrackSuffix.Match(title);
            var label = FromPrefix.Replace(attribution.Groups["label"].Value, "").Trim();
            var artists = split.Item2.Select(Canonical).Where(a => a.Length > 0).ToArray();
            facts = new CandidateFacts {
                Title = title, Album = Canonical(song.album?.name), Artists = artists,
                BaseTitle = Canonical(attribution.Success ? title.Substring(0, attribution.Index) : title),
                Source = ShowSource(label, Canonical(song.album?.name)),
                Attribution = Canonical(label), ExplicitMedium = attribution.Success && SourceMedium.IsMatch(label),
                Soundtrack = SoundtrackAlbum.IsMatch(song.album?.name ?? ""),
                Excluded = Edition.IsMatch(label) || AttributionVariant.IsMatch(label) ||
                    SoundtrackVariant.IsMatch(song.album?.name ?? "")
            };
            request.Facts.Add(song, facts);
            return facts;
        }

        private static Match EvaluateTier(SearchResultSong song, TrackIdentity request, TrackIdentity alternate,
            IReadOnlyDictionary<long, string[]> aliases, MatchingTier tier)
        {
            Match match;
            switch (tier)
            {
                case MatchingTier.Ordinary: match = Evaluate(song, request); break;
                case MatchingTier.ArtistAlias: match = Evaluate(song, request, aliases); break;
                case MatchingTier.TrackPrefix:
                    // The prefix relaxation never borrows artist aliases.
                    match = Evaluate(song, alternate);
                    if (match != null) match.Notes = string.Join("/", new[] { match.Notes, "TrackNo-confirmed title prefix" }.Where(n => n.Length > 0));
                    break;
                case MatchingTier.TranslatedSoundtrack: match = Evaluate(song, request, allowLocalizedSoundtrack: true); break;
                case MatchingTier.ShowCreditedSoundtrack: match = ShowCredited(song, request); break;
                default: return null;
            }
            if (match != null) match.Tier = tier;
            return match;
        }

        private static Match ShowCredited(SearchResultSong song, TrackIdentity request)
        {
            // Cheap title/length checks precede the attribution and work-name checks.
            if (song == null || song.id <= 0 || string.IsNullOrWhiteSpace(song.name) || song.name.Length > 512 ||
                !request.Duration.HasValue || request.Duration <= 0 || song.duration <= 0 ||
                Math.Abs((double)request.Duration.Value - song.duration) > 2000 ||
                request.Artists.Length == 0 || request.Album.Length == 0 || request.PrefixIdentity) return null;
            var facts = Facts(song, request);
            if (facts.BaseTitle != request.Title || !facts.ExplicitMedium || !facts.Soundtrack || facts.Source == null ||
                facts.Excluded || SoundtrackVariant.IsMatch(request.RawAlbum ?? "") ||
                SoundtrackSuffix.IsMatch(request.RawTitle) || facts.Artists.Length == 0) return null;
            var localArtists = request.Artists.Select(Canonical).ToArray();
            if (facts.Artists.Any(a => localArtists.Contains(a))) return null;
            // Empty/malformed credits must not disappear while checking that EVERY artist names a work.
            if ((song.artists ?? new SearchResultArtist[0]).Any(a => string.IsNullOrWhiteSpace(a?.name))) return null;
            if (!facts.Artists.All(a => WorkNameLongEnough(a) &&
                (facts.Attribution.Contains(a) || facts.Album.Contains(a) || request.Album.Contains(a)))) return null;
            var alias = new[] { song.album?.alias, song.album?.tns, song.album?.transNames }
                .SelectMany(names => (names ?? new string[0]).Take(16)).Take(32)
                .Any(name => name != null && name.Length <= 512 && Canonical(name) == request.Album);
            if (!alias && !facts.Artists.Any(a => request.Album.Contains(a))) return null;
            return new Match {
                Song = song, SoundtrackSource = facts.Source, Notes = "", ExactTitleAndArtist = true,
                Identity = request.Title + "\0work:" + facts.Source,
                AlbumScore = Similarity(request.Album, facts.Album),
                DurationDifference = Math.Abs((double)request.Duration.Value - song.duration) / 1000,
                Score = 95 + 5 * Similarity(request.Album, facts.Album)
            };
        }

        private static bool WorkNameLongEnough(string name) => LatinLetters.Matches(name).Count >= 4 || CjkLetters.Matches(name).Count >= 2;

        private static string ShowSource(string label, string album)
        {
            var medium = SourceMedium.Match(label);
            if (!medium.Success) return null;
            var work = Canonical(label.Substring(medium.Length));
            return WorkNameLongEnough(work) && album.Contains(work) ? work : null;
        }
    }
}
