namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models
{
    public class IncludedAttributes
    {
        // genres, people
        public string Name { get; set; }

        // categories
        public string Title { get; set; }

        // mediaStaff
        public string Role { get; set; }
    }
}
