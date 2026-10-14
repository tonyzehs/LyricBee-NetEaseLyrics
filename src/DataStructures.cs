using System.Collections.Generic;
#pragma warning disable 649 // Suppresses: ___ is never assigned to

// ReSharper disable All

namespace MusicBeePlugin
{
    internal class ApiResponse
    {
        public int code;
    }

    internal class SearchResult : ApiResponse
    {
        public SearchResultResult result;
    }

    internal class SearchResultResult
    {
        public int? songCount;
        public IEnumerable<SearchResultSong> songs;
    }

    internal class SearchResultSong
    {
        public string name;
        public long id;
        public long duration; // in ms
        public SearchResultAlbum album;
        public SearchResultArtist[] artists;
    }

    internal class SearchResultAlbum 
    {
        public long id;
        public string name;
    }

    internal class SearchResultArtist
    {
        public long id;
        public string name;
    }

    internal class LyricResult : ApiResponse
    {
        public bool nolyric;
        public LyricInner lrc;
        public LyricInner tlyric;
        public LyricInner romalrc;
    }

    internal class ArtistDetailsResponse : ApiResponse
    {
        public ArtistDetails artist;
    }

    internal class ArtistDetails
    {
        public long id;
        public string name;
        public string[] alias;
    }

    internal class LanguageResponse : ApiResponse
    {
        public Newtonsoft.Json.Linq.JObject data;
    }

    internal class LyricInner
    {
        public string lyric;
    }
}
