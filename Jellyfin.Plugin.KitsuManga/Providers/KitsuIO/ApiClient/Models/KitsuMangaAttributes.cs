using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models
{
    public class KitsuMangaAttributes : IKitsuAttributesWithTitles
    {
        public string Synopsis { get; set; }
        public string CanonicalTitle { get; set; }
        public KitsuTitles Titles { get; set; }
        public List<string> AbbreviatedTitles { get; set; }
        public string AverageRating { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public string AgeRating { get; set; }
        public string Serialization { get; set; }
        public KitsuImage PosterImage { get; set; }
        public KitsuImage CoverImage { get; set; }
    }
}
