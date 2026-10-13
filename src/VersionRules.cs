using System;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static class VersionRules
    {
        internal static bool TryParse(string value, bool stableOnly, out Version version,
            out bool development, bool allowTagPrefix = true)
        {
            version = null; development = false;
            if (value == null) return false;
            var match = Regex.Match(value, @"^(v)?([0-9]+\.[0-9]+\.[0-9]+)(-(?:test\.[0-9]+|pre(?:\.?[0-9]+)?))?$");
            if (!match.Success || (!allowTagPrefix && match.Groups[1].Success) ||
                !Version.TryParse(match.Groups[2].Value, out version)) return false;
            development = match.Groups[3].Success;
            return !stableOnly || !development;
        }

        internal static bool IsDevelopment(string value) =>
            TryParse(value, false, out _, out var development, allowTagPrefix: false) && development;
    }
}
