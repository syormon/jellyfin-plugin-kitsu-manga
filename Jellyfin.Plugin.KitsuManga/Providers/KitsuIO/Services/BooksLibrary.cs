using System;
using System.IO;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;

/// <summary>
/// Folders exist in every kind of library, these helpers limit the plugin to the ones of Books libraries.
/// </summary>
internal static class BooksLibrary
{
    private static readonly TimeSpan _locationsLifetime = TimeSpan.FromSeconds(30);
    private static readonly char[] _separators = ['/', '\\'];
    private static string[] _locations = [];
    private static DateTime _locationsExpire = DateTime.MinValue;

    /// <summary>
    /// Whether a path is inside a Books library. The root folders of the library are not.
    /// </summary>
    public static bool Contains(ILibraryManager libraryManager, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return GetLocations(libraryManager).Any(location =>
            path.Length > location.Length + 1
            && path.StartsWith(location, StringComparison.OrdinalIgnoreCase)
            && _separators.Contains(path[location.Length]));
    }

    /// <summary>
    /// Whether an item is a folder that can hold the volumes / chapters of a manga.
    /// </summary>
    public static bool IsMangaFolder(ILibraryManager libraryManager, BaseItem item)
        => item != null && item.GetType() == typeof(Folder) && Contains(libraryManager, item.Path);

    /// <summary>
    /// Gets the Kitsu id of the manga folder containing a path, if any.
    /// </summary>
    public static string FindMangaFolderId(ILibraryManager libraryManager, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        for (var folder = Path.GetDirectoryName(path); Contains(libraryManager, folder); folder = Path.GetDirectoryName(folder))
        {
            var item = libraryManager.FindByPath(folder, true);
            if (item != null && item.TryGetProviderId(Plugin.ProviderId, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    private static string[] GetLocations(ILibraryManager libraryManager)
    {
        if (DateTime.UtcNow > _locationsExpire)
        {
            _locations = libraryManager.GetVirtualFolders()
                .Where(x => x.CollectionType == CollectionTypeOptions.books && x.Locations != null)
                .SelectMany(x => x.Locations)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x.TrimEnd(_separators))
                .ToArray();
            _locationsExpire = DateTime.UtcNow + _locationsLifetime;
        }

        return _locations;
    }
}
