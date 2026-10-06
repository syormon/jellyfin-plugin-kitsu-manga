using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.KitsuManga.Configuration;
using Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;

public static class TitleSelector
{
    public static string GetOriginalTitle(IKitsuAttributesWithTitles attributes, string libraryLanguage)
        => GetFirstTitleByPreference(attributes, Plugin.Instance.Configuration.OriginalTitlePreference, libraryLanguage);

    public static string GetTitle(IKitsuAttributesWithTitles attributes, string libraryLanguage)
        => GetFirstTitleByPreference(attributes, Plugin.Instance.Configuration.TitlePreference, libraryLanguage);

    /// <summary>
    /// Gets the titles in every language, for matching.
    /// </summary>
    public static ICollection<string> GetAllTitles(IKitsuAttributesWithTitles attributes)
        => (attributes.Titles?.Values ?? Enumerable.Empty<string>())
            .Append(attributes.CanonicalTitle)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

    static string GetFirstTitleByPreference(IKitsuAttributesWithTitles attribute, TitlePreferenceType preference, string libraryLanguage) =>
        GetTitlesOrderedByPreference(attribute, preference, libraryLanguage).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? string.Empty;

    static IEnumerable<string> GetTitlesOrderedByPreference(IKitsuAttributesWithTitles attribute, TitlePreferenceType preference, string libraryLanguage) =>
        preference switch
        {
            TitlePreferenceType.English => [attribute.Titles?.En, attribute.Titles?.EnUs, attribute.CanonicalTitle, attribute.Titles?.EnJp, attribute.Titles?.JaJp],
            TitlePreferenceType.Japanese => [attribute.Titles?.JaJp, attribute.Titles?.EnJp, attribute.CanonicalTitle, attribute.Titles?.En, attribute.Titles?.EnUs,],
            TitlePreferenceType.JapaneseRomaji => [attribute.Titles?.EnJp, attribute.CanonicalTitle, attribute.Titles?.JaJp, attribute.Titles?.En, attribute.Titles?.EnUs,],
            TitlePreferenceType.Canonical => [attribute.CanonicalTitle, attribute.Titles?.EnJp, attribute.Titles?.JaJp, attribute.Titles?.En, attribute.Titles?.EnUs,],
            // Kitsu has titles in many languages; fall back to English when the library language is missing
            TitlePreferenceType.LibraryLanguage => (attribute.Titles?.GetByLanguage(libraryLanguage) ?? Enumerable.Empty<string>())
                .Concat(GetTitlesOrderedByPreference(attribute, TitlePreferenceType.English, libraryLanguage)),
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null)
        };
}
