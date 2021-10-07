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
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Models.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
        public IActionResult Forecast()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            return View("~/Plugins/Misc.SalesForecasting/Views/ForecastSearch.cshtml", new ForecastSearchModel());
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
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
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Forecast(ForecastSearchModel searchModel)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();

            var forecast = await _salesForecastingService.ForecastAsync(searchModel).ConfigureAwait(false);
            var resultModel = new ForecastResultModel
            {
                ResultsJson = JsonConvert.SerializeObject(forecast, _jsonSerializerSettings)
            };
            resultModel.SetGridPageSize();
            return View("~/Plugins/Misc.SalesForecasting/Views/ForecastResults.cshtml", resultModel);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
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
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult ExportCsv(string resultsJson)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();
            var results = JsonConvert.DeserializeObject<ForecastResponse[]>(resultsJson);
            return Ok();
        }
    }
}
