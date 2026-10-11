namespace MusicBeePlugin
{
    internal static class SearchMatchLegacy
    {
        public static long QueryWithFeatRemoved(string trackTitle, string artist, bool fuzzy, LookupSession session)
        {
            // The legacy option may relax spelling, but must not accept the first unrelated result.
            return SearchMatch.SearchAndMatch(trackTitle, artist, "", null, session, fuzzy);
        }
    }
}
