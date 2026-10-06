namespace Jellyfin.Plugin.KitsuManga.Providers.KitsuIO.ApiClient.Models
{
    public class Included
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public IncludedAttributes Attributes { get; set; }
        public IncludedRelationships Relationships { get; set; }
    }

    public class IncludedRelationships
    {
        public KitsuRelationship Person { get; set; }
    }

    public class KitsuRelationship
    {
        public KitsuResourceIdentifier Data { get; set; }
    }

    public class KitsuResourceIdentifier
    {
        public string Id { get; set; }
        public string Type { get; set; }
    }
}
