using Newtonsoft.Json;
using RouletteRecorder.Dalamud.Network.DungeonLogger.Structures;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RouletteRecorder.Dalamud.Network.DungeonLogger
{
    public sealed class DungeonLoggerClient : IDisposable
    {
        private readonly HttpClient client;
        private readonly CookieContainer cookieContainer;
        private readonly DungeonLoggerApiMode apiMode;

        public DungeonLoggerClient(string serverUrl, DungeonLoggerApiMode apiMode = DungeonLoggerApiMode.New)
        {
            this.apiMode = apiMode;
            cookieContainer = new CookieContainer();
            client = new HttpClient(new HttpClientHandler()
            {
                CookieContainer = cookieContainer,
                AutomaticDecompression = DecompressionMethods.All,
            })
            {
                BaseAddress = NormalizeBaseAddress(serverUrl),
                Timeout = TimeSpan.FromSeconds(15),
            };
        }

        public async Task<Response<object>?> PostLogin(string password, string username)
        {
            var response = await client.PostAsync("api/login", JsonContent(new
            {
                password,
                username
            }));
            return await ReadResponse<object>(response);
        }

        public async Task<Response<object>?> PostRecord(DungeonLoggerRecord record)
        {
            object body = apiMode == DungeonLoggerApiMode.Legacy
                ? new
                {
                    mazeId = record.MazeId,
                    profKey = record.ProfKey,
                }
                : record;

            var response = await client.PostAsync("api/record", JsonContent(body));
            return await ReadResponse<object>(response);
        }

        public async Task<Response<List<StatProf>>?> GetStatProf()
        {
            var response = await client.GetAsync("api/stat/prof");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<Response<List<StatProf>>>(content);
        }

        public async Task<Response<List<StatMaze>>?> GetStatMaze()
        {
            var response = await client.GetAsync("api/stat/maze");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<Response<List<StatMaze>>>(content);
        }

        private static StringContent JsonContent(object body)
        {
            return new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        }

        private static async Task<Response<T>?> ReadResponse<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return JsonConvert.DeserializeObject<Response<T>>(content);
                }
                catch
                {
                    return new Response<T>()
                    {
                        Data = default,
                        Code = 0,
                        Msg = "Malformed server response",
                    };
                }
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<Response<object>>(content);
                if (parsed != null && parsed.Msg is { Length: > 0 })
                {
                    return new Response<T>()
                    {
                        Data = default,
                        Code = parsed.Code,
                        Msg = parsed.Msg,
                    };
                }
            }
            catch
            {
                // fall through to the legacy error format below
            }

            try
            {
                var legacyError = JsonConvert.DeserializeObject<ErrorResponse>(content);
                if (legacyError != null)
                {
                    return new Response<T>()
                    {
                        Data = default,
                        Code = legacyError.Code,
                        Msg = string.Join(",", legacyError.Msg),
                    };
                }
            }
            catch
            {
                // fall through to the generic HTTP error below
            }

            return new Response<T>()
            {
                Data = default,
                Code = (int)response.StatusCode,
                Msg = $"HTTP {(int)response.StatusCode}",
            };
        }

        private static Uri NormalizeBaseAddress(string serverUrl)
        {
            var value = serverUrl?.Trim() ?? string.Empty;
            if (!value.EndsWith('/')) value += "/";

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Server URL must be an absolute http(s) address", nameof(serverUrl));
            }

            return uri;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
