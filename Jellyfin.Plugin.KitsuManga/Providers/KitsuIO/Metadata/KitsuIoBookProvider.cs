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
    /// A book inside a manga folder is a volume / chapter of that manga: it is only linked to the manga.
    /// A book without a manga folder, e.g. a one-shot at the root of the library, gets the metadata of the manga itself.
    /// </summary>
    public class KitsuIoBookProvider : IRemoteMetadataProvider<Book, BookInfo>, IHasOrder
    {
        private readonly ILogger<KitsuIoBookProvider> _log;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILibraryManager _libraryManager;
        public int Order => -4;
        public string Name => Plugin.ProviderName;

        public KitsuIoBookProvider(ILogger<KitsuIoBookProvider> logger, IHttpClientFactory httpClientFactory, ILibraryManager libraryManager)
        {
            _log = logger;
            _httpClientFactory = httpClientFactory;
            _libraryManager = libraryManager;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BookInfo searchInfo, CancellationToken cancellationToken)
        {
            // The name is what the user typed into the identify dialog, so it goes first
            return await MangaLookup.Search(searchInfo, _httpClientFactory, cancellationToken, searchInfo.Name, searchInfo.SeriesName, MangaLookup.GetFolderName(searchInfo.Path));
        }

        public async Task<MetadataResult<Book>> GetMetadata(BookInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Book>();

            var folderId = BooksLibrary.FindMangaFolderId(_libraryManager, info.Path);
            var isVolume = folderId != null;

            var kitsuId = folderId ?? await MangaLookup.ResolveId(info.ProviderIds.GetValueOrDefault(Plugin.ProviderId), _httpClientFactory, cancellationToken);
            if (string.IsNullOrWhiteSpace(kitsuId))
            {
                kitsuId = await MangaLookup.FindMangaId(_log, _httpClientFactory, cancellationToken, info.SeriesName, info.Name, MangaLookup.GetFolderName(info.Path));
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

            var title = TitleSelector.GetTitle(mangaInfo.Data.Attributes, info.MetadataLanguage);

            result.HasMetadata = true;
            result.Item = new Book
            {
                // A file named after a volume / chapter keeps its own name, only the series name is replaced
                Name = isVolume || MangaLookup.HasVolumeInfo(info.Name) ? null : title,
                SeriesName = title
            };
            MangaLookup.ApplyCommon(result.Item, mangaInfo, kitsuId);
            ApplyNumbers(result.Item, info, title, MangaLookup.GetAllTitles(mangaInfo.Data.Attributes));
            if (!isVolume)
            {
                MangaLookup.ApplyDetails(result, mangaInfo, info.MetadataLanguage);
            }

            return result;
        }

        /// <summary>
        /// Numbers the book, so the volumes / chapters of a manga sort in reading order whatever their files are called.
        /// </summary>
        private static void ApplyNumbers(Book book, BookInfo info, string title, IEnumerable<string> titles)
        {
            var (volume, chapter) = VolumeParser.Parse(info.Path, titles);
            if (volume == null && chapter == null)
            {
                return;
            }

            // Index numbers are whole numbers, chapter 10.5 keeps its place through the sort name
            if (chapter != null)
            {
                book.IndexNumber = (int)chapter.Value;
                book.ParentIndexNumber = (int?)volume;
            }
            else
            {
                book.IndexNumber = (int)volume.Value;
            }

            book.ForcedSortName = VolumeParser.GetSortName(title, volume, chapter);
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);

            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
