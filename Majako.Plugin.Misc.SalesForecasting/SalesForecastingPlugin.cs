using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task ManageSiteMapAsync(SiteMapNode rootNode)
    {
      var salesNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Sales");
      if (salesNode == null)
        return;
      salesNode.ChildNodes.Insert(salesNode.ChildNodes.Count, new SiteMapNode
      {
        Title = await _localizationService.GetResourceAsync("Majako.Plugin.Misc.SalesForecasting.SalesForecasting"),
        Url = $"/{BASE_ROUTE}/{FORECAST}",
        Visible = true,
        RouteValues = new RouteValueDictionary { { "Area", "Admin" } },
        IconClass = "far fa-dot-circle",
        SystemName = "Misc.SalesForecasting"
      });
    }

    public override async Task InstallAsync()
    {
      var settings = await _settingService.LoadSettingAsync<SalesForecastingPluginSettings>();
      var settingsTask = _settingService.SaveSettingAsync(settings);
      //Rickard: can you check commented code, it run forever.
      //await Task.WhenAll(await GetLocalizationsAsync().Select(async t =>
      //{
      //  using var streamReader = t.file.OpenText();
      //  await _localizationService.ImportResourcesFromXmlAsync(t.language, streamReader);
      //}).ToArrayAsync());
      await settingsTask;
      await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
      var settingsTask = _settingService.DeleteSettingAsync<SalesForecastingPluginSettings>();
      await _localizationService.DeleteLocaleResourcesAsync("Majako.Plugin.Misc.SalesForecasting");
      await settingsTask;
      await base.UninstallAsync();
    }

    private async IAsyncEnumerable<(FileInfo file, Language language)> GetLocalizationsAsync()
    {
      var pluginsDirectory = _nopFileProvider.MapPath(NopPluginDefaults.Path);
      var files = Directory
          .EnumerateFiles(
              Path.Combine(pluginsDirectory, SYSTEM_NAME, "resources"),
              "*.xml")
          .Select(x => new FileInfo(x))
          .ToDictionary(x => Path.GetFileNameWithoutExtension(x.Name).ToLower());
      var languages = (await _languageService
          .GetAllLanguagesAsync())
          .ToLookup(x => x.LanguageCulture.ToLower());

      static string getLanguageCode(string culture) => culture.Split('-', 1)[0];

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
