using System;
using System.Collections.Generic;
using Jellyfin.Plugin.KitsuManga.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.KitsuManga
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public const string ProviderName = "Kitsu Manga";

        public const string ProviderId = "KitsuManga";

        public override string Name => ProviderName;

        public override Guid Id => Guid.Parse("cfc137d6-36fd-4971-8528-dd501ede393b");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = string.Format("{0}.Configuration.configPage.html", GetType().Namespace)
                }
            };
        }
    }
}
