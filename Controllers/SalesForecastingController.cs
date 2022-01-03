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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

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
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();

            return View("~/Plugins/Misc.SalesForecasting/Views/Configure.cshtml", settings);
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Forecast()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            return string.IsNullOrEmpty(settings.ForecastId)
              ? View("~/Plugins/Misc.SalesForecasting/Views/ForecastSearch.cshtml", new ForecastSearchModel())
              : await GetForecast();
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(SalesForecastingPluginSettings settings)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            _settingService.SaveSetting(settings);

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return View("~/Plugins/Misc.SalesForecasting/Views/Configure.cshtml", settings);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Forecast(ForecastSearchModel searchModel)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            await _salesForecastingService.SubmitForecastAsync(searchModel).ConfigureAwait(false);
            return View("~/Plugins/Misc.SalesForecasting/Views/ForecastSubmitted.cshtml");
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> NewForecast()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            settings.ForecastId = null;
            _settingService.SaveSetting(settings);

            return await Forecast();
        }

        [HttpGet]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> GetForecast()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            try
            {
                var forecast = await _salesForecastingService.GetForecastAsync().ConfigureAwait(false);
                var resultModel = new ForecastResultModel
                {
                    ResultsJson = JsonConvert.SerializeObject(forecast, _jsonSerializerSettings)
                };
                resultModel.SetGridPageSize();
                return View("~/Plugins/Misc.SalesForecasting/Views/ForecastResults.cshtml", resultModel);
            }
            catch (System.Exception)
            {
                settings.ForecastId = null;
                _settingService.SaveSetting(settings);
                _notificationService.ErrorNotification(_localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastNotFound"));
                return await Forecast();
            }
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public IActionResult GetResultsPage(ForecastResultModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var results = JsonConvert.DeserializeObject<ForecastResponse[]>(model.ResultsJson);
            return Json(new ForecastListModel().PrepareToGrid(
                model,
                new PagedList<ForecastResponse>(
                    results,
                    model.Page,
                    model.PageSize,
                    results.Length),
                () => results.Skip((model.Page - 1) * model.PageSize).Take(model.PageSize)));
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AutoValidateAntiforgeryToken]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> ExportCsv()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            var forecast = await _salesForecastingService.GetForecastAsync().ConfigureAwait(false);
            var stream = new MemoryStream();
            var header = string.Join(';', new[]
            {
                "Majako.Plugin.Misc.SalesForecasting.ProductName",
                "Majako.Plugin.Misc.SalesForecasting.ProductId",
                "Majako.Plugin.Misc.SalesForecasting.Sku",
                "Majako.Plugin.Misc.SalesForecasting.Prediction"
            }.Select(_localizationService.GetResource));
            using (var streamWriter = new StreamWriter(stream))
            {
                streamWriter.WriteLine(header);
                foreach (var line in forecast)
                    streamWriter.WriteLine($"{line.Name};{line.ProductId};{line.Sku};{line.Prediction}");
            }
            return File(stream.ToArray(), "application/csv", $"sales_forecast_{DateTime.UtcNow.ToShortDateString()}.csv");
        }
    }
}
