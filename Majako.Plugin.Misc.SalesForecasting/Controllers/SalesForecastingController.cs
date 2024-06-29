using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Majako.Services.Factories;
using Majako.Services.Models;
using Majako.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Majako.Plugin.Misc.SalesForecasting.Controllers
{
    public class SalesForecastingController : BasePluginController
    {
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISalesForecastModelFactory _salesForecastModelFactory;
        private readonly ISettingService _settingService;
        private readonly ISalesForecastingService _salesForecastingService;

        public SalesForecastingController(
            ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISalesForecastModelFactory salesForecastModelFactory,
            ISettingService settingService,
            ISalesForecastingService salesForecastingService
            )
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _salesForecastingService = salesForecastingService;
            _salesForecastModelFactory = salesForecastModelFactory;
            _settingService = settingService;
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
            var model = await _salesForecastModelFactory.PrepareProductSearchModelAsync(new ForecastProductSearchModel());
            return string.IsNullOrEmpty(settings.ForecastId)
        ? View("~/Plugins/Misc.SalesForecasting/Views/ForecastSearch.cshtml", model)
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
        public async Task<IActionResult> GetPreliminary(ForecastProductSearchModel searchModel)
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

            var forecast = (await _salesForecastingService.GetForecastAsync().ConfigureAwait(false))?.ToArray() ?? Array.Empty<ForecastResponseModel>();
            var results = new PagedList<ForecastResponseModel>(
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
          "Majako.Plugin.Misc.SalesForecasting.Prediction",
          "Majako.Plugin.Misc.SalesForecasting.QuantilePrediction"
              }.Select(async resource => await _localizationService.GetResourceAsync(resource))));
            using (var streamWriter = new StreamWriter(stream))
            {
                await streamWriter.WriteLineAsync(header);
                foreach (var line in forecast)
                    await streamWriter.WriteLineAsync($"\"{line.Name}\";{line.ProductId};{line.Sku};{line.Prediction};{line.QuantilePrediction}");
            }
            return File(stream.ToArray(), "application/csv", $"sales_forecast_{DateTime.UtcNow.ToShortDateString()}.csv");
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> ExportSalesCsv(ForecastProductSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var sales = await _salesForecastingService.GetDataAsync(searchModel);
            var stream = new MemoryStream();
            var header = string.Join(';', new[]
            {
        "ProductId",
        "Quantity",
        "Created",
        "Discount"
      });
            using (var streamWriter = new StreamWriter(stream))
            {
                streamWriter.WriteLine(header);
                foreach (var line in sales)
                    streamWriter.WriteLine($"{line.ProductId};{line.Quantity};{line.Created};{line.Discount}");
            }
            return File(stream.ToArray(), "application/csv", $"sales_{DateTime.UtcNow.ToShortDateString()}.csv");
        }
    }
}
