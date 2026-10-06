using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.Services;

/// <summary>
/// Reads the volume and chapter number of a book from the name of its file and folders.
/// </summary>
internal static class VolumeParser
{
    private const string _number = @"(\d+(?:\.\d+)?)";
    private const string _wordStart = @"(?<![\p{L}\p{N}])";

    // Bracketed tags, e.g. "(2019)", "[Digital]", "{Scans}", "【完】"
    private static readonly Regex _bracketsRegex = new(@"[\(\[\{【][^\)\]\}】]*[\)\]\}】]", RegexOptions.Compiled);

    // "v01", "Vol. 3", "Volume 12", "Tome 2"
    private static readonly Regex _volumeRegex = new(
        _wordStart + @"(?:volume|vol|v|tome|t)[\s_\.]*" + _number,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "c001", "Ch. 5", "Chapter 12.5", "#4"
    private static readonly Regex _chapterRegex = new(
        @"(?:" + _wordStart + @"(?:chapter|chap|ch|c)|#)[\s_\.]*" + _number,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "第1巻", "12巻"
    private static readonly Regex _japaneseVolumeRegex = new(@"第?\s*" + _number + @"\s*[巻卷冊集]", RegexOptions.Compiled);

    // "第3話", "12話"
    private static readonly Regex _japaneseChapterRegex = new(@"第?\s*" + _number + @"\s*話", RegexOptions.Compiled);

    // "One Piece 001", "One Piece - 12"
    private static readonly Regex _trailingNumberRegex = new(@"(?<![\p{L}\p{N}\.])" + _number + @"\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Gets the volume and chapter number of a book.
    /// </summary>
    /// <param name="path">The path of the book.</param>
    /// <param name="seriesNames">The names of the manga, a number in them is not a volume ("Mob Psycho 100").</param>
    public static (double? Volume, double? Chapter) Parse(string path, IEnumerable<string> seriesNames)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (null, null);
        }

        var name = Clean(Path.GetFileNameWithoutExtension(path));
        var volume = Match(name, _volumeRegex) ?? Match(name, _japaneseVolumeRegex);
        var chapter = Match(name, _chapterRegex) ?? Match(name, _japaneseChapterRegex);

        if (volume == null && chapter == null)
        {
            // A bare number is taken as the volume
            volume = Match(RemoveSeriesName(name, seriesNames), _trailingNumberRegex);
        }

        if (volume == null)
        {
            // Chapters can be grouped in a folder per volume, e.g. "One Piece/Volume 01/Chapter 001.cbz"
            var folder = Clean(Path.GetFileName(Path.GetDirectoryName(path)));
            volume = Match(folder, _volumeRegex) ?? Match(folder, _japaneseVolumeRegex);
        }

        return (volume, chapter);
    }

    /// <summary>
    /// Gets a name that sorts books by volume, then chapter. Books of a volume without a chapter number come first.
    /// </summary>
    public static string GetSortName(string seriesName, double? volume, double? chapter)
    {
        var sortName = new StringBuilder(seriesName?.ToLowerInvariant());
        if (volume != null)
        {
            sortName.Append(" v").Append(volume.Value.ToString("00000.###", CultureInfo.InvariantCulture));
        }

        if (chapter != null)
        {
            sortName.Append(" c").Append(chapter.Value.ToString("00000.###", CultureInfo.InvariantCulture));
        }

        return sortName.ToString().Trim();
    }

    private static string Clean(string name)
        => string.IsNullOrEmpty(name)
            ? string.Empty
            // Full-width characters, e.g. "１" or "（", become their regular counterparts
            : _bracketsRegex.Replace(name.Normalize(NormalizationForm.FormKC), " ").Trim();

    private static string RemoveSeriesName(string name, IEnumerable<string> seriesNames)
    {
        var seriesName = seriesNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Normalize(NormalizationForm.FormKC).Trim())
            .OrderByDescending(x => x.Length)
            .FirstOrDefault(x => name.StartsWith(x, StringComparison.OrdinalIgnoreCase));

        return seriesName == null ? name : name[seriesName.Length..];
    }

    private static double? Match(string name, Regex regex)
    {
        var match = regex.Match(name);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
