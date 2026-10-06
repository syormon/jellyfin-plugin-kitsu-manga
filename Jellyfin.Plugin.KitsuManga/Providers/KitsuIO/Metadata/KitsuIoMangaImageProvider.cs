using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient;
using MediaBrowser.Common.Net;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Metadata
{
    public class KitsuIoMangaImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryManager _libraryManager;
        public string Name => Plugin.ProviderName;

        public KitsuIoMangaImageProvider(IHttpClientFactory httpClientFactory, ILibraryManager libraryManager)
        {
            _httpClientFactory = httpClientFactory;
            _libraryManager = libraryManager;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);

            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!item.TryGetProviderId(Plugin.ProviderId, out var mangaId) || string.IsNullOrWhiteSpace(mangaId))
            {
                return list;
            }

            // The artwork belongs to the manga folder, its volumes / chapters keep their own covers
            if (item is Book && BooksLibrary.FindMangaFolderId(_libraryManager, item.Path) != null)
            {
                return list;
            }

            var mangaInfo = await KitsuIoApi.Get_Manga(mangaId, _httpClientFactory, cancellationToken);

            var primaryImage = mangaInfo?.Data?.Attributes?.PosterImage?.Original?.ToString();
            if (!string.IsNullOrWhiteSpace(primaryImage))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = primaryImage,
                    Type = ImageType.Primary
                });
            }

            var backdropImage = mangaInfo?.Data?.Attributes?.CoverImage?.Original?.ToString();
            if (!string.IsNullOrWhiteSpace(backdropImage))
            {
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = backdropImage,
                    Type = ImageType.Backdrop
                });
            }

            return list;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[]
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }

        public bool Supports(BaseItem item)
        {
            return item is Book || BooksLibrary.IsMangaFolder(_libraryManager, item);
        }
    }
}
