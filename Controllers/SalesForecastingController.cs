using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Majako.Plugin.Misc.SalesForecasting.Services;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Models.Extensions;
using System;
using System.IO;

namespace Majako.Plugin.Misc.SalesForecasting.Controllers
{
  public class SalesForecastingController : BasePluginController
  {
    private readonly IPermissionService _permissionService;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly SalesForecastingService _salesForecastingService;
    private readonly ILocalizationService _localizationService;

    public SalesForecastingController(
        IPermissionService permissionService,
        ISettingService settingService,
        INotificationService notificationService,
        SalesForecastingService salesForecastingService,
        ILocalizationService localizationService)
    {
      _permissionService = permissionService;
      _settingService = settingService;
      _notificationService = notificationService;
      _salesForecastingService = salesForecastingService;
      _localizationService = localizationService;
    }

    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> Configure()
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
        return AccessDeniedView();

      var settings = await _settingService.LoadSettingAsync<SalesForecastingPluginSettings>();

      return View("~/Plugins/Misc.SalesForecasting/Views/Configure.cshtml", settings);
    }

    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> Forecast()
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      var settings = await _settingService.LoadSettingAsync<SalesForecastingPluginSettings>();
      return string.IsNullOrEmpty(settings.ForecastId)
        ? View("~/Plugins/Misc.SalesForecasting/Views/ForecastSearch.cshtml", new ForecastSearchModel())
        : await GetResults();
    }

    [HttpPost]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> Configure(SalesForecastingPluginSettings settings)
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
        return AccessDeniedView();

      await _settingService.SaveSettingAsync(settings);

      _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

      return View("~/Plugins/Misc.SalesForecasting/Views/Configure.cshtml", settings);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> Forecast([FromBody] ForecastSubmissionModel model)
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      await _salesForecastingService.SubmitForecastAsync(model).ConfigureAwait(false);
      return Ok();
    }

    [HttpPost]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> GetPreliminary(ForecastSearchModel searchModel)
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      var model = await _salesForecastingService.GetPreliminaryData(searchModel);
      return View("~/Plugins/Misc.SalesForecasting/Views/Preliminary.cshtml", model);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> NewForecast()
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      var settings = await _settingService.LoadSettingAsync<SalesForecastingPluginSettings>();
      settings.ForecastId = null;
      await _settingService.SaveSettingAsync(settings);

      return await Forecast();
    }

    [HttpGet]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> GetResults()
    {
      try
      {
        var forecast = await _salesForecastingService.GetForecastAsync().ConfigureAwait(false);
        if (forecast == null)
          _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Majako.Plugin.Misc.SalesForecasting.ForecastNotReady"));
      }
      catch (Exception)
      {
        _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Majako.Plugin.Misc.SalesForecasting.ForecastNotFound"));
        return await NewForecast();
      }
      var resultModel = new ForecastResultModel();
      resultModel.SetGridPageSize();
      return View("~/Plugins/Misc.SalesForecasting/Views/ForecastResults.cshtml", resultModel);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> GetResultsPage(ForecastResultModel model)
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      var forecast = (await _salesForecastingService.GetForecastAsync().ConfigureAwait(false))?.ToArray() ?? Array.Empty<ForecastResponse>();
      var results = new PagedList<ForecastResponse>(
        forecast,
        model.Page - 1,
        model.PageSize);
      return Json(new ForecastListModel().PrepareToGrid(model, results, () => results));
    }

    [HttpPost]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Area(AreaNames.Admin)]
    public async Task<IActionResult> ExportCsv()
    {
      if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
        return AccessDeniedView();

      var forecast = await _salesForecastingService.GetForecastAsync().ConfigureAwait(false);
      var stream = new MemoryStream();

      var header = string.Join(';',
        await Task.WhenAll(new[]
        {
          "Majako.Plugin.Misc.SalesForecasting.ProductName",
          "Majako.Plugin.Misc.SalesForecasting.ProductId",
          "Majako.Plugin.Misc.SalesForecasting.Sku",
          "Majako.Plugin.Misc.SalesForecasting.Prediction"
        }.Select(async resource => await _localizationService.GetResourceAsync(resource))));
      using (var streamWriter = new StreamWriter(stream))
      {
        await streamWriter.WriteLineAsync(header);
        foreach (var line in forecast)
          await streamWriter.WriteLineAsync($"\"{line.Name}\";{line.ProductId};{line.Sku};{line.Prediction}");
      }
      return File(stream.ToArray(), "application/csv", $"sales_forecast_{DateTime.UtcNow.ToShortDateString()}.csv");
    }
  }
}
