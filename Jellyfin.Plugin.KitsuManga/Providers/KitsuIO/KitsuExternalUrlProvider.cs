using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO;

public class KitsuExternalUrlProvider : IExternalUrlProvider
{
    public string Name => Plugin.ProviderName;

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is Book or Folder && item.TryGetProviderId(Plugin.ProviderId, out var externalId))
        {
            yield return $"https://kitsu.app/manga/{externalId}";
        }
    }
}
