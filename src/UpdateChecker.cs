using System;
using System.Net.Http;
using System.Linq;
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
        internal bool RateLimited { get; set; }
    }

    internal sealed class UpdateChecker
    {
        internal const string LatestReleaseApi = "https://api.github.com/repos/tonyzehs/LyricBee-NetEaseLyrics/releases/latest";
        internal const int MaximumResponseBytes = 256 * 1024;
        private readonly Func<HttpMessageHandler> _handlerFactory;
        private readonly Action<string> _trace;

        internal UpdateChecker(Func<HttpMessageHandler> handlerFactory = null, Action<string> trace = null)
        {
            _handlerFactory = handlerFactory ?? (() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
            _trace = trace;
        }

        internal static bool TryVersion(string value, bool stableOnly, out Version version, out bool development)
            => VersionRules.TryParse(value, stableOnly, out version, out development);

        private UpdateResult Failed(string reason, bool rateLimited = false)
        {
            try { _trace?.Invoke("Update check failed: " + reason); } catch { }
            return new UpdateResult { RateLimited = rateLimited };
        }

        internal async Task<UpdateResult> CheckAsync(string installedVersion, CancellationToken cancellationToken)
        {
            if (!TryVersion(installedVersion, false, out var installed, out var development)) return Failed("unreadable version");
            try
            {
                using (var client = new HttpClient(_handlerFactory()) { Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = MaximumResponseBytes })
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi))
                {
                    request.Headers.UserAgent.ParseAdd("LyricBee-NetEaseLyrics");
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                    using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var limited = (int)response.StatusCode == 429 || ((int)response.StatusCode == 403 &&
                                response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Any(v => v.Trim() == "0"));
                            return Failed("HTTP " + (int)response.StatusCode, limited);
                        }
                        // Also check the buffered bytes: some non-Windows test hosts ignore the client cap.
                        if ((await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).Length > MaximumResponseBytes)
                            return Failed("unreadable reply");
                        var body = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        cancellationToken.ThrowIfCancellationRequested();
                        if (body["draft"]?.Type != JTokenType.Boolean || body["draft"].Value<bool>() ||
                            body["prerelease"]?.Type != JTokenType.Boolean || body["prerelease"].Value<bool>() ||
                            body["tag_name"]?.Type != JTokenType.String) return Failed("unreadable reply");
                        var tag = body["tag_name"].Value<string>();
                        if (!TryVersion(tag, true, out var latest, out _)) return Failed("unreadable reply");
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
            catch (OperationCanceledException) { return Failed(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
            catch (Newtonsoft.Json.JsonException) { return Failed("unreadable reply"); }
            catch (HttpRequestException) { return Failed("connection failure"); }
            catch { return Failed("connection failure"); }
        }
    }
}
