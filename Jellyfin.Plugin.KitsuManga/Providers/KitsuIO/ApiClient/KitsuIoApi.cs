using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models;
using MediaBrowser.Common.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient
{
    internal class KitsuIoApi
    {
        private const string _apiBaseUrl = "https://kitsu.app/api/edge";
        private const string _mangaIncludes = "include=genres,categories,staff.person"
            + "&fields[genres]=name&fields[categories]=title&fields[mediaStaff]=role,person&fields[people]=name";
        private static readonly JsonSerializerOptions _serializerOptions;

        // Every volume / chapter of a manga asks for the same manga
        private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, (DateTime Expires, object Response)> _cache = new();

        static KitsuIoApi()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            _serializerOptions.Converters.Add(new LongToStringConverter());
            _serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public static Task<ApiResponse<List<KitsuMangaItem>>> Search_Manga(string text, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
        {
            return Get<ApiResponse<List<KitsuMangaItem>>>($"/manga?filter[text]={Uri.EscapeDataString(text)}&page[limit]=10", httpClientFactory, cancellationToken);
        }

        public static Task<ApiResponse<List<KitsuMangaItem>>> Search_Manga_By_Slug(string slug, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
        {
            return Get<ApiResponse<List<KitsuMangaItem>>>($"/manga?filter[slug]={Uri.EscapeDataString(slug)}&page[limit]=1", httpClientFactory, cancellationToken);
        }

        public static Task<ApiResponse<KitsuMangaItem>> Get_Manga(string mangaId, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
        {
            return Get<ApiResponse<KitsuMangaItem>>($"/manga/{Uri.EscapeDataString(mangaId)}?{_mangaIncludes}", httpClientFactory, cancellationToken);
        }

        private static async Task<T> Get<T>(string pathAndQuery, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(pathAndQuery, out var cached) && cached.Expires > DateTime.UtcNow)
            {
                return (T)cached.Response;
            }

            var result = await Fetch<T>(pathAndQuery, httpClientFactory, cancellationToken).ConfigureAwait(false);

            if (_cache.Count > 500)
            {
                _cache.Clear();
            }

            _cache[pathAndQuery] = (DateTime.UtcNow + _cacheLifetime, result);
            return result;
        }

        private static async Task<T> Fetch<T>(string pathAndQuery, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
        {
            var httpClient = httpClientFactory.CreateClient(NamedClient.Default);

            using var request = new HttpRequestMessage(HttpMethod.Get, _apiBaseUrl + pathAndQuery);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(responseStream, _serializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
