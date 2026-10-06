using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Metadata
{
    /// <summary>
    /// A folder of a Books library that is named after a manga is that manga: it gets the metadata and artwork,
    /// the books inside it are its volumes / chapters.
    /// </summary>
    public class KitsuIoMangaProvider : IRemoteMetadataProvider<Folder, ItemLookupInfo>, IHasOrder
    {
        private readonly ILogger<KitsuIoMangaProvider> _log;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryManager _libraryManager;
        public int Order => -4;
        public string Name => Plugin.ProviderName;

        public KitsuIoMangaProvider(ILogger<KitsuIoMangaProvider> logger, IHttpClientFactory httpClientFactory, ILibraryManager libraryManager)
        {
            _log = logger;
            _httpClientFactory = httpClientFactory;
            _libraryManager = libraryManager;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            return await MangaLookup.Search(searchInfo, _httpClientFactory, cancellationToken, searchInfo.Name);
        }

        public async Task<MetadataResult<Folder>> GetMetadata(ItemLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Folder>();

            // Folders exist in every kind of library
            if (!BooksLibrary.Contains(_libraryManager, info.Path))
            {
                return result;
            }

            var kitsuId = await MangaLookup.ResolveId(info.ProviderIds.GetValueOrDefault(Plugin.ProviderId), _httpClientFactory, cancellationToken);
            if (string.IsNullOrWhiteSpace(kitsuId))
            {
                // A folder inside a manga is not another manga
                if (BooksLibrary.FindMangaFolderId(_libraryManager, info.Path) != null)
                {
                    return result;
                }

                kitsuId = await MangaLookup.FindMangaId(_log, _httpClientFactory, cancellationToken, info.Name);
            }

            if (string.IsNullOrWhiteSpace(kitsuId))
            {
                return result;
            }

            var mangaInfo = await KitsuIoApi.Get_Manga(kitsuId, _httpClientFactory, cancellationToken);
            if (mangaInfo?.Data?.Attributes == null)
            {
                return result;
            }

            result.HasMetadata = true;
            result.Item = new Folder
            {
                Name = TitleSelector.GetTitle(mangaInfo.Data.Attributes, info.MetadataLanguage)
            };
            MangaLookup.ApplyCommon(result.Item, mangaInfo, kitsuId);
            MangaLookup.ApplyDetails(result, mangaInfo, info.MetadataLanguage);

            return result;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);

            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
