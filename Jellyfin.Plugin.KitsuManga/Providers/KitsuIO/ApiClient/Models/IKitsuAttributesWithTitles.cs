using System.Collections.Generic;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models;

public interface IKitsuAttributesWithTitles
{
    public string CanonicalTitle { get; set; }
    public KitsuTitles Titles { get; set; }
}
