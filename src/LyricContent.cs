using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MusicBeePlugin
{
    internal static partial class LyricProcessor
    {
        // Used only to recognise credits, never to discard or retime a real lyric line.
        private static readonly Regex CreditTimePrefix = new Regex(
            @"^(?:\[[0-9]+:[0-9.:+\-]+\]\s*)+", RegexOptions.CultureInvariant);

        private static string CreditHeaderForDisplay(string line) =>
            IsCreditHeader(line) ? CreditTimePrefix.Replace(line.Trim(), "") : line;

        internal static bool IsCreditOnlyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var lines = NormalizeLines(text).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
            return lines.Any(IsCreditHeader) && lines.All(IsHeader);
        }

        private static bool IsCreditHeader(string line)
        {
            line = line.Trim();
            if (CreditLine.IsMatch(CreditTimePrefix.Replace(line, ""))) return true;
            if (!line.StartsWith("{") || line.Length > 8192) return false;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(line)) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    var header = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read() || header.Properties().Any(p => p.Name != "t" && p.Name != "c") ||
                        header["t"]?.Type != JTokenType.Integer || !(header["c"] is JArray pieces) ||
                        pieces.Count == 0 || pieces.Count > 16) return false;
                    var text = "";
                    foreach (var piece in pieces)
                    {
                        if (!(piece is JObject part) || part["tx"]?.Type != JTokenType.String ||
                            part.Properties().Any(p => p.Name != "tx" && p.Name != "li" && p.Name != "or")) return false;
                        var value = (string)part["tx"];
                        if (value.Length > 1024 || value.IndexOfAny(new[] { '\r', '\n' }) >= 0) return false;
                        text += value;
                    }
                    return text.Length <= 4096 && CreditLine.IsMatch(text.Trim());
                }
            }
            catch (JsonException) { return false; }
        }
    }
}
