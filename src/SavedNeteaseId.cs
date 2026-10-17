using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal enum SavedIdStatus { Absent, Valid, Unreadable, TitleMismatch, LengthMismatch, NoLyrics }

    internal static class SavedNeteaseId
    {
        internal const string Prefix = "163 key(Don't modify):";
        internal const int MaximumCommentLength = 8192;
        // NetEase's download metadata key, also used in NCM metadata.
        // Verified against five real FLAC comments on 2026-10-14; no account key is involved.
        private static readonly byte[] MetadataKey = {
            0x23, 0x31, 0x34, 0x6c, 0x6a, 0x6b, 0x5f, 0x21, 0x5c, 0x5d, 0x26, 0x30, 0x55, 0x3c, 0x27, 0x28
        };

        internal static SavedIdStatus Read(string comment, string title, long? duration, out long id)
        {
            id = 0;
            if (comment == null || !comment.StartsWith(Prefix, StringComparison.Ordinal)) return SavedIdStatus.Absent;
            if (comment.Length > MaximumCommentLength) return SavedIdStatus.Unreadable;
            try
            {
                var encrypted = Convert.FromBase64String(Regex.Replace(comment.Substring(Prefix.Length), @"[\s\x00]+$", ""));
                if (encrypted.Length == 0 || encrypted.Length % 16 != 0) return SavedIdStatus.Unreadable;
                string plain;
                using (var aes = Aes.Create())
                {
                    aes.Key = MetadataKey; aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.PKCS7;
                    using (var decrypt = aes.CreateDecryptor())
                        plain = new UTF8Encoding(false, true).GetString(decrypt.TransformFinalBlock(encrypted, 0, encrypted.Length));
                }
                if (!plain.StartsWith("music:", StringComparison.Ordinal)) return SavedIdStatus.Unreadable;
                JObject metadata;
                using (var input = new StringReader(plain.Substring(6)))
                using (var reader = new JsonTextReader(input) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
                {
                    metadata = JObject.Load(reader);
                    if (reader.Read()) return SavedIdStatus.Unreadable;
                }
                if (!TryReadSongId(metadata["musicId"], out var saved))
                    return SavedIdStatus.Unreadable;
                if (metadata["musicName"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(title) ||
                    SearchMatch.Canonical((string)metadata["musicName"]).Length == 0 ||
                    SearchMatch.Canonical((string)metadata["musicName"]) != SearchMatch.Canonical(title))
                    return SavedIdStatus.TitleMismatch;
                var length = metadata["duration"];
                if (!duration.HasValue || duration <= 0 || length == null ||
                    (length.Type != JTokenType.Integer && length.Type != JTokenType.Float) ||
                    !decimal.TryParse(length.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) ||
                    milliseconds <= 0 || Math.Abs(milliseconds - duration.Value) > 2000)
                    return SavedIdStatus.LengthMismatch;
                id = saved;
                return SavedIdStatus.Valid;
            }
            catch (Exception) { return SavedIdStatus.Unreadable; }
        }

        private static bool TryReadSongId(JToken token, out long id)
        {
            id = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.String)) return false;
            var digits = token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
            if (string.IsNullOrEmpty(digits) || digits.Length > 19 || digits[0] == '0') return false;
            foreach (var digit in digits)
                if (digit < '0' || digit > '9') return false;
            return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
        }
    }
}
