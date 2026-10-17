using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicBeePlugin
{
    internal static partial class SearchMatch
    {
        private static Match ShowCreditMatch(SearchResultSong song, TrackIdentity request, CandidateFacts facts, string notes = "") =>
            new Match {
                Song = song, SoundtrackSource = facts.Source, Notes = notes, ExactTitleAndArtist = true,
                Identity = request.Title + "\0work:" + facts.Source,
                AlbumScore = Similarity(request.Album, facts.Album),
                DurationDifference = Math.Abs((double)request.Duration.Value - song.duration) / 1000,
                Score = 95 + 5 * Similarity(request.Album, facts.Album), Tier = MatchingTier.ShowCreditedSoundtrack
            };

        private static Match[] IncludeSoundtrackAlbumPeers(SearchResultSong[] songs, TrackIdentity request, Match[] anchors)
        {
            if (anchors.Length == 0) return anchors;
            var matches = new List<Match>(anchors);
            var knownIds = new HashSet<long>(anchors.Select(a => a.Song.id));
            foreach (var song in songs)
            {
                if (knownIds.Contains(song.id)) continue;
                var facts = ShowCreditShape(song, request);
                if (facts == null || !facts.Artists.All(WorkNameLongEnough)) continue;
                // Only the original fully checked entries may anchor another listing.
                // A newly admitted entry cannot introduce more aliases or performers.
                if (!anchors.Any(anchor => {
                    var other = Facts(anchor.Song, request);
                    return string.Equals(facts.Title, other.Title, StringComparison.OrdinalIgnoreCase) &&
                        facts.Attribution == other.Attribution && facts.Source == other.Source &&
                        string.Equals(NormalizeText(song.album?.name), NormalizeText(anchor.Song.album?.name), StringComparison.OrdinalIgnoreCase) &&
                        facts.Artists.Length <= other.Artists.Length &&
                        Math.Abs((double)song.duration - anchor.Song.duration) <= 2000 &&
                        song.artists.Any(a => a.id > 0 && anchor.Song.artists.Any(b => b.id == a.id));
                })) continue;
                matches.Add(ShowCreditMatch(song, request, facts, "same-album soundtrack"));
                knownIds.Add(song.id);
            }
            return matches.ToArray();
        }
    }
}
