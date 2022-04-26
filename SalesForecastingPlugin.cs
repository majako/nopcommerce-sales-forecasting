using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Services.Common;
using Nop.Services.Plugins;
using Nop.Web.Framework.Menu;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Core.Infrastructure;
using System.IO;
using Nop.Core.Domain.Localization;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public class SalesForecastingPlugin : BasePlugin, IMiscPlugin, IAdminMenuPlugin
    {
        public const string SYSTEM_NAME = "Misc.SalesForecasting";
        public const string BASE_ROUTE = "Admin/SalesForecasting";
        public const string CONFIGURE = "Configure";
        public const string FORECAST = "Forecast";

        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly ILanguageService _languageService;
        private readonly INopFileProvider _nopFileProvider;
        private readonly ILocalizationService _localizationService;

        public SalesForecastingPlugin(
            IWebHelper webHelper,
            ISettingService settingService,
            ILanguageService languageService,
            INopFileProvider nopFileProvider,
            ILocalizationService localizationService)
        {
            _webHelper = webHelper;
            _settingService = settingService;
            _languageService = languageService;
            _nopFileProvider = nopFileProvider;
            _localizationService = localizationService;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}{BASE_ROUTE}/{CONFIGURE}";
        }

        public void ManageSiteMap(SiteMapNode rootNode)
        {
            var salesNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Sales");
            if (salesNode == null)
                return;
            salesNode.ChildNodes.Insert(salesNode.ChildNodes.Count, new SiteMapNode
            {
                Title = _localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.SalesForecasting"),
                Url = $"/{BASE_ROUTE}/{FORECAST}",
                Visible = true,
                RouteValues = new RouteValueDictionary { { "Area", "Admin" } },
                IconClass = "fa-dot-circle-o",
                SystemName = "Order.SalesForecasting"
            });
        }

        public override void Install()
        {
            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            _settingService.SaveSetting(settings);
            foreach (var (file, language) in GetLocalizations())
            {
                using (var streamReader = file.OpenText())
                {
                    _localizationService.ImportResourcesFromXml(language, streamReader);
                }
            }
            base.Install();
        }

        public override void Uninstall()
        {
            _settingService.DeleteSetting<SalesForecastingPluginSettings>();
            var resources = GetLocalizations()
                .SelectMany(x => _localizationService.GetAllResources(x.language.Id))
                .Where(x => x.ResourceName.StartsWith("Majako.Plugin.Misc.SalesForecasting", System.StringComparison.InvariantCultureIgnoreCase));
            foreach (var resource in resources)
                _localizationService.DeleteLocaleStringResource(resource);
            base.Uninstall();
        }

        private IEnumerable<(FileInfo file, Language language)> GetLocalizations()
        {
            var pluginsDirectory = _nopFileProvider.MapPath(NopPluginDefaults.Path);
            var files = Directory
                .EnumerateFiles(
                    Path.Combine(pluginsDirectory, SYSTEM_NAME, "resources"),
                    "*.xml")
                .Select(x => new FileInfo(x))
                .ToDictionary(x => Path.GetFileNameWithoutExtension(x.Name).ToLower());
            var languages = _languageService
                .GetAllLanguages()
                .ToLookup(x => x.LanguageCulture.ToLower());

            string getLanguageCode(string culture) => culture.Split('-', 1)[0];

            var filesByLanguageCode = files
              .GroupBy(x => getLanguageCode(x.Key))
              .ToDictionary(g => g.Key, g => g.First().Value);

            foreach (var group in languages)
            {
                var languageCode = getLanguageCode(group.Key);
                foreach (var language in group)
                {
                    if (files.TryGetValue(group.Key, out var file) || filesByLanguageCode.TryGetValue(languageCode, out file))
                        yield return (file, language);
                }
            }
        }
    }
}
