using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MusicBeePlugin
{
    internal sealed class UpdateResult
    {
        internal bool Succeeded { get; set; }
        internal bool UpdateAvailable { get; set; }
        internal string LatestVersion { get; set; }
        internal string ReleasePage { get; set; }
    }

    internal sealed class UpdateChecker
    {
        internal const string LatestReleaseApi = "https://api.github.com/repos/tonyzehs/LyricBee-NetEaseLyrics/releases/latest";
        private readonly Func<HttpMessageHandler> _handlerFactory;

        internal UpdateChecker(Func<HttpMessageHandler> handlerFactory = null)
        {
            _handlerFactory = handlerFactory ?? (() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }

        internal static bool TryVersion(string value, bool stableOnly, out Version version, out bool development)
        {
            version = null; development = false;
            if (value == null) return false;
            var match = Regex.Match(value, @"^v?([0-9]+\.[0-9]+\.[0-9]+)(-(?:test\.[0-9]+|pre(?:\.?[0-9]+)?))?$");
            if (!match.Success || !Version.TryParse(match.Groups[1].Value, out version)) return false;
            development = match.Groups[2].Success;
            return !stableOnly || !development;
        }

        internal async Task<UpdateResult> CheckAsync(string installedVersion, CancellationToken cancellationToken)
        {
            if (!TryVersion(installedVersion, false, out var installed, out var development)) return new UpdateResult();
            try
            {
                using (var client = new HttpClient(_handlerFactory()) { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = 65536 })
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi))
                {
                    request.Headers.UserAgent.ParseAdd("LyricBee-NetEaseLyrics");
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                    using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode) return new UpdateResult();
                        var body = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        cancellationToken.ThrowIfCancellationRequested();
                        if (body["draft"]?.Type != JTokenType.Boolean || body["draft"].Value<bool>() ||
                            body["prerelease"]?.Type != JTokenType.Boolean || body["prerelease"].Value<bool>() ||
                            body["tag_name"]?.Type != JTokenType.String) return new UpdateResult();
                        var tag = body["tag_name"].Value<string>();
                        if (!TryVersion(tag, true, out var latest, out _)) return new UpdateResult();
                        return new UpdateResult
                        {
                            Succeeded = true,
                            UpdateAvailable = latest > installed || (latest == installed && development),
                            LatestVersion = latest.ToString(3),
                            // Build the URL from the validated version, rather than executing a URL from the response.
                            ReleasePage = "https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/" + Uri.EscapeDataString(tag)
                        };
                    }
                }
            }
            catch { return new UpdateResult(); }
        }
    }
}
