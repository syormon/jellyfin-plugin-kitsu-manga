using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;

/// <summary>
/// Matches the names of folders and books to a Kitsu manga and maps its metadata.
/// </summary>
internal static class MangaLookup
{
    // Bracketed tags, e.g. "(2019)", "[Digital]", "{Scans}", "【完】"
    private static readonly Regex _bracketsRegex = new(@"[\(\[\{【][^\)\]\}】]*[\)\]\}】]", RegexOptions.Compiled);

    // Volume / chapter markers and everything after them, e.g. "v01", "Vol. 3", "Volume 12", "Ch. 5", "c001", "#4"
    private static readonly Regex _volumeRegex = new(
        @"(^|[\s_\-,\.]+)(v|vol|volume|c|ch|chap|chapter|t|tome|#)[\s_\.]*\d+(\.\d+)?([\s_\-,\.].*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Japanese volume / chapter markers and everything after them, e.g. "第1巻", "12巻", "第3話"
    private static readonly Regex _japaneseVolumeRegex = new(@"[\s_\-]*第?\s*\d+(\.\d+)?\s*[巻卷冊集話](.*)?$", RegexOptions.Compiled);

    // Trailing bare number, e.g. "One Piece 001", "One Piece - 12"
    private static readonly Regex _trailingNumberRegex = new(@"[\s_]+-?[\s_]*\d+(\.\d+)?$", RegexOptions.Compiled);

    public static string GetFolderName(string path)
        => string.IsNullOrEmpty(path) ? null : Path.GetFileName(Path.GetDirectoryName(path));

    /// <summary>
    /// Whether a name carries a volume / chapter number.
    /// </summary>
    public static bool HasVolumeInfo(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        name = _bracketsRegex.Replace(name.Normalize(NormalizationForm.FormKC), " ").Trim();
        return _volumeRegex.IsMatch(name) || _japaneseVolumeRegex.IsMatch(name) || _trailingNumberRegex.IsMatch(name);
    }

    /// <summary>
    /// Gets the Kitsu id from what was entered as id by hand: the id itself, the slug or the url of the manga,
    /// e.g. "38", "one-piece" or "https://kitsu.app/manga/one-piece".
    /// </summary>
    public static async Task<string> ResolveId(string id, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        id = id.Trim().TrimEnd('/');
        id = id[(id.LastIndexOf('/') + 1)..];
        if (id.Length == 0 || id.All(char.IsAsciiDigit))
        {
            return id.Length == 0 ? null : id;
        }

        var response = await KitsuIoApi.Search_Manga_By_Slug(id, httpClientFactory, cancellationToken);
        return response?.Data?.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Finds the manga with a title that is exactly one of the names, most likely name first.
    /// </summary>
    public static async Task<string> FindMangaId(ILogger log, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken, params string[] names)
    {
        foreach (var searchName in GetSearchNames(names))
        {
            log.LogInformation("Start KitsuIo... Searching({Name})", searchName);
            var apiResponse = await KitsuIoApi.Search_Manga(searchName, httpClientFactory, cancellationToken);

            // Books libraries can also contain non-manga books and other folders, so only accept an exact title match
            var match = apiResponse?.Data?.FirstOrDefault(manga => manga.Attributes != null && GetAllTitles(manga.Attributes)
                .Any(x => string.Equals(NormalizeTitle(x), NormalizeTitle(searchName), StringComparison.OrdinalIgnoreCase)));
            if (match != null)
            {
                return match.Id.ToString(CultureInfo.InvariantCulture);
            }
        }

        log.LogInformation("KitsuIo... No manga found for {Name}", names.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
        return null;
    }

    /// <summary>
    /// Gets the results for the identify dialog.
    /// </summary>
    public static async Task<List<RemoteSearchResult>> Search(ItemLookupInfo searchInfo, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken, params string[] names)
    {
        var searchResults = new List<RemoteSearchResult>();

        if (searchInfo.TryGetProviderId(Plugin.ProviderId, out var mangaId) && !string.IsNullOrWhiteSpace(mangaId))
        {
            mangaId = await ResolveId(mangaId, httpClientFactory, cancellationToken);
            var manga = mangaId == null ? null : await KitsuIoApi.Get_Manga(mangaId, httpClientFactory, cancellationToken);
            if (manga?.Data?.Attributes != null)
            {
                searchResults.Add(MapToRemoteSearchResult(manga.Data, searchInfo.MetadataLanguage));
            }

            return searchResults;
        }

        foreach (var searchName in GetSearchNames(names))
        {
            var searchResponse = await KitsuIoApi.Search_Manga(searchName, httpClientFactory, cancellationToken);
            var parsedSearchResponse = searchResponse?.Data?.Where(x => x.Attributes != null).Select(x => MapToRemoteSearchResult(x, searchInfo.MetadataLanguage)).ToList();
            if (parsedSearchResponse?.Count > 0)
            {
                searchResults.AddRange(parsedSearchResponse);
                break;
            }
        }

        return searchResults;
    }

    /// <summary>
    /// Maps the metadata that is shared by a manga and its volumes / chapters.
    /// </summary>
    public static void ApplyCommon(BaseItem item, ApiResponse<KitsuMangaItem> manga, string kitsuId)
    {
        var categories = GetIncludedNames(manga, "categories", x => x.Title);
        var genres = GetIncludedNames(manga, "genres", x => x.Name);
        if (genres.Length == 0)
        {
            // Newer entries only have categories
            genres = categories;
        }

        item.ProviderIds = new Dictionary<string, string>() {{Plugin.ProviderId, kitsuId}};
        item.OfficialRating = manga.Data.Attributes.AgeRating;
        item.Genres = genres;
        item.Tags = categories.Except(genres, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Maps the metadata of the manga itself.
    /// </summary>
    public static void ApplyDetails<T>(MetadataResult<T> result, ApiResponse<KitsuMangaItem> manga, string language)
        where T : BaseItem
    {
        var attributes = manga.Data.Attributes;
        var item = result.Item;

        item.OriginalTitle = TitleSelector.GetOriginalTitle(attributes, language);
        item.Overview = attributes.Synopsis;
        // KitsuIO has a max rating of 100
        item.CommunityRating = float.TryParse(attributes.AverageRating, NumberStyles.Float, CultureInfo.InvariantCulture, out var rating)
            ? MathF.Round(rating / 10, 1)
            : null;
        item.ProductionYear = attributes.StartDate?.Year;
        item.PremiereDate = attributes.StartDate?.DateTime;
        item.EndDate = attributes.EndDate?.DateTime;

        if (!string.IsNullOrWhiteSpace(attributes.Serialization))
        {
            item.AddStudio(attributes.Serialization);
        }

        foreach (var person in GetStaff(manga))
        {
            result.AddPerson(person);
        }
    }

    /// <summary>
    /// Gets the possible manga names from the names of a folder or book.
    /// </summary>
    private static List<string> GetSearchNames(string[] names)
    {
        return names
            // A trailing number can be part of the title ("Mob Psycho 100") or a volume ("One Piece 001")
            .SelectMany(x => new[] { CleanName(x, false), CleanName(x, true) })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CleanName(string name, bool removeTrailingNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Full-width characters, e.g. "１" or "（", become their regular counterparts
        name = _bracketsRegex.Replace(name.Normalize(NormalizationForm.FormKC), " ");
        name = _volumeRegex.Replace(name, string.Empty);
        name = _japaneseVolumeRegex.Replace(name, string.Empty);
        if (removeTrailingNumber)
        {
            name = _trailingNumberRegex.Replace(name.Trim(), string.Empty);
        }

        return name.Replace('_', ' ').Trim(' ', '-', ',', '.');
    }

    private static string NormalizeTitle(string title)
        => new string(title.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>
    /// Gets every title of a manga, in every language.
    /// </summary>
    public static IEnumerable<string> GetAllTitles(KitsuMangaAttributes attributes)
        => TitleSelector.GetAllTitles(attributes)
            .Concat(attributes.AbbreviatedTitles ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static string[] GetIncludedNames(ApiResponse<KitsuMangaItem> manga, string type, Func<IncludedAttributes, string> selector)
        => manga.Included?
               .Where(x => x.Type == type && x.Attributes != null)
               .Select(x => selector(x.Attributes))
               .Where(x => !string.IsNullOrWhiteSpace(x))
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToArray()
           ?? Array.Empty<string>();

    private static IEnumerable<PersonInfo> GetStaff(ApiResponse<KitsuMangaItem> manga)
    {
        if (manga.Included == null)
        {
            yield break;
        }

        var people = manga.Included
            .Where(x => x.Type == "people" && !string.IsNullOrWhiteSpace(x.Attributes?.Name))
            .GroupBy(x => x.Id)
            .ToDictionary(x => x.Key, x => x.First().Attributes.Name);

        foreach (var staff in manga.Included.Where(x => x.Type == "mediaStaff"))
        {
            var personId = staff.Relationships?.Person?.Data?.Id;
            if (personId == null || !people.TryGetValue(personId, out var name))
            {
                continue;
            }

            var role = staff.Attributes?.Role ?? string.Empty;
            yield return new PersonInfo
            {
                Name = name,
                Role = role,
                Type = role.Contains("Story", StringComparison.OrdinalIgnoreCase) ? PersonKind.Author
                    : role.Contains("Art", StringComparison.OrdinalIgnoreCase) ? PersonKind.Illustrator
                    : PersonKind.Unknown
            };
        }
    }

    private static RemoteSearchResult MapToRemoteSearchResult(KitsuMangaItem manga, string language)
    {
        var parsedManga = new RemoteSearchResult
        {
            Name = TitleSelector.GetTitle(manga.Attributes, language),
            SearchProviderName = Plugin.ProviderName,
            ImageUrl = manga.Attributes.PosterImage?.Medium?.ToString(),
            Overview = manga.Attributes.Synopsis,
            ProductionYear = manga.Attributes.StartDate?.Year,
            PremiereDate = manga.Attributes.StartDate?.DateTime
        };
        parsedManga.SetProviderId(Plugin.ProviderId, manga.Id.ToString(CultureInfo.InvariantCulture));

        return parsedManga;
    }
}
