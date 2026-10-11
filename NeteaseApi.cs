using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace MusicBeePlugin
{
    /* Encryption protocol adopted from ZonyLrcToolsX; see LICENSE-Zony. */
    internal static class NeteaseApi
    {
        public static IEnumerable<SearchResultSong> Search(string query, LookupSession session)
        {
            if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<SearchResultSong>();
            return session.SearchCached(query, () =>
            {
                var data = new Dictionary<string, object>
                {
                    { "csrf_token", "" }, { "s", query }, { "offset", 0 }, { "type", 1 }, { "limit", 20 }
                };
                var result = RequestNewApi<SearchResult>(session, "search/new",
                    "https://music.163.com/weapi/search/get", data, ValidSearch);
                if (result == null)
                    result = session.Request<SearchResult>("search/legacy", HttpMethod.Get,
                        "https://music.163.com/api/search/get/?csrf_token=hlpretag=&hlposttag=&s=" +
                        Uri.EscapeDataString(query) + "&type=1&offset=0&total=true&limit=6", null, ValidSearch);
                return (result?.result?.songs ?? Enumerable.Empty<SearchResultSong>())
                    .Where(song => song != null && song.id > 0 && !string.IsNullOrWhiteSpace(song.name)).ToArray();
            });
        }

        private static bool ValidSearch(SearchResult result)
        {
            return result != null && result.code == 200 && result.result != null &&
                result.result.songCount.HasValue && result.result.songCount >= 0 &&
                (result.result.songCount == 0 || result.result.songs != null);
        }

        public static LyricResult RequestLyric(long id, LookupSession session)
        {
            if (id <= 0) return null;
            var data = new Dictionary<string, object>
            {
                { "os", "pc" }, { "id", id }, { "lv", -1 }, { "kv", -1 }, { "tv", -1 }, { "rv", -1 }
            };
            var result = RequestNewApi<LyricResult>(session, "lyrics/new",
                "https://music.163.com/weapi/song/lyric?csrf_token=", data, ValidLyrics);
            if (result != null) return result;
            return session.Request<LyricResult>("lyrics/legacy", HttpMethod.Get,
                "https://music.163.com/api/song/lyric?os=pc&id=" + id + "&lv=-1&kv=-1&tv=-1", null, ValidLyrics);
        }

        private static bool ValidLyrics(LyricResult result)
        {
            // An explicit instrumental response is a valid absence. A bare code=200 is not.
            return result != null && result.code == 200 &&
                (result.nolyric || !string.IsNullOrWhiteSpace(result.lrc?.lyric));
        }

        private static T RequestNewApi<T>(LookupSession session, string label, string url,
            Dictionary<string, object> data, Func<T, bool> validate) where T : class
        {
            using (var content = new FormUrlEncodedContent(EncryptRequest(data)))
                return session.Request(label, HttpMethod.Post, url, content, validate);
        }

        private static Dictionary<string, string> EncryptRequest(object data)
        {
            var secret = NeteaseMusicEncryptionHelper.CreateSecretKey(16);
            return new Dictionary<string, string>
            {
                { "params", NeteaseMusicEncryptionHelper.AesEncode(
                    NeteaseMusicEncryptionHelper.AesEncode(JsonConvert.SerializeObject(data),
                        NeteaseMusicEncryptionHelper.Nonce), secret) },
                { "encSecKey", NeteaseMusicEncryptionHelper.RsaEncode(secret) }
            };
        }
    }

    // A synchronous MusicBee callback gets one client and one deadline, including all fallbacks.
    // The handler seam lets regression tests exercise the real request and parsing code offline.
    internal sealed class LookupSession : IDisposable
    {
        private readonly HttpClient _client;
        private readonly CancellationTokenSource _deadline;
        private readonly TimeSpan _requestTimeout;
        private readonly Action<string> _trace;
        private readonly Dictionary<string, SearchResultSong[]> _searches = new Dictionary<string, SearchResultSong[]>();

        internal LookupSession(Action<string> trace = null, HttpMessageHandler handler = null,
            TimeSpan? requestTimeout = null, TimeSpan? lookupTimeout = null)
        {
            _trace = trace;
            _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(8);
            _deadline = new CancellationTokenSource(lookupTimeout ?? TimeSpan.FromSeconds(30));
            _client = handler == null ? new HttpClient() : new HttpClient(handler);
            _client.Timeout = Timeout.InfiniteTimeSpan;
            _client.MaxResponseContentBufferSize = 4 * 1024 * 1024;
            _client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", "appver=1.5.0.75771;");
        }

        internal IEnumerable<SearchResultSong> SearchCached(string query, Func<SearchResultSong[]> search)
        {
            if (!_searches.TryGetValue(query, out var result))
                _searches[query] = result = search();
            return result;
        }

        internal T Request<T>(string label, HttpMethod method, string url, HttpContent content,
            Func<T, bool> validate) where T : class
        {
            if (_deadline.IsCancellationRequested) return null;
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token))
                using (var request = new HttpRequestMessage(method, url) { Content = content })
                {
                    timeout.CancelAfter(_requestTimeout);
                    using (var response = _client.SendAsync(request, timeout.Token).GetAwaiter().GetResult())
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Trace(label + ": HTTP " + (int)response.StatusCode);
                            return null;
                        }
                        var result = JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                        if (validate(result)) return result;
                        var code = (result as ApiResponse)?.code;
                        Trace(label + ": rejected or incomplete response" +
                            (code.HasValue ? " (code " + code.Value + ")" : ""));
                        return null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Trace(label + (_deadline.IsCancellationRequested ? ": lookup deadline reached" : ": request timed out"));
            }
            catch (Exception ex)
            {
                // Do not log response bodies, URLs, song tags or local file paths.
                Trace(label + ": " + ex.GetType().Name);
            }
            return null;
        }

        private void Trace(string message)
        {
            try { _trace?.Invoke(message); } catch { /* Diagnostics must not break a lookup. */ }
        }

        public void Dispose()
        {
            _deadline.Dispose();
            _client.Dispose();
        }
    }
}
