using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models
{
    /// <summary>
    /// Titles by Kitsu locale, e.g. "en", "en_us", "en_jp" (romaji), "ja_jp", "fr_fr".
    /// </summary>
    public class KitsuTitles : Dictionary<string, string>
    {
        private const string _romajiLocale = "en_jp";

        public KitsuTitles() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public string En => this.GetValueOrDefault("en");

        public string EnJp => this.GetValueOrDefault(_romajiLocale);

        public string JaJp => this.GetValueOrDefault("ja_jp");

        public string EnUs => this.GetValueOrDefault("en_us");

        /// <summary>
        /// Gets the titles written in a language.
        /// </summary>
        /// <param name="language">Two letter ISO language, optionally followed by a country, e.g. "ja" or "pt-BR".</param>
        public IEnumerable<string> GetByLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return Enumerable.Empty<string>();
            }

            var locale = language.Trim().Replace('-', '_');
            var languageCode = locale.Split('_')[0];

            return this
                .Where(x => !string.Equals(x.Key, _romajiLocale, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(x.Key.Split('_')[0], languageCode, StringComparison.OrdinalIgnoreCase))
                // Exact locale first, then the bare language, then other countries
                .OrderBy(x => string.Equals(x.Key, locale, StringComparison.OrdinalIgnoreCase) ? 0 : x.Key.Contains('_') ? 2 : 1)
                .Select(x => x.Value);
        }
    }
}
