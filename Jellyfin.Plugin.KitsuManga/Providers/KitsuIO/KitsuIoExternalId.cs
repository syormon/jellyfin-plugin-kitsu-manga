using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO
{
    public class KitsuIoExternalId : IExternalId
    {
        private readonly ILibraryManager _libraryManager;

        public KitsuIoExternalId(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public bool Supports(IHasProviderIds item)
            => item is Book || BooksLibrary.IsMangaFolder(_libraryManager, item as BaseItem);

        public string ProviderName
            => Plugin.ProviderName;

        public string Key
            => Plugin.ProviderId;

        public ExternalIdMediaType? Type
            => null;
    }
}
