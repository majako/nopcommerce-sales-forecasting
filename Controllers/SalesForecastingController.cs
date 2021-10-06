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
            if (searchModel.PeriodLength < 15)
            {
                return Json(new ForecastListModel().PrepareToGrid(
                        searchModel.ProductSearchModel,
                        new PagedList<ForecastResponse>(
                            Enumerable.Empty<ForecastResponse>().AsQueryable(),
                            searchModel.ProductSearchModel.Page,
                            searchModel.ProductSearchModel.PageSize,
                            0),
                        () => Enumerable.Empty<ForecastResponse>()));
            }
            var forecast = await _salesForecastingService.ForecastAsync(searchModel.PeriodLength, searchModel.ProductSearchModel).ConfigureAwait(false);
            var model = new ForecastListModel().PrepareToGrid(
                searchModel.ProductSearchModel,
                new PagedList<ForecastResponse>(
                    forecast.AsQueryable(),
                    searchModel.ProductSearchModel.Page,
                    searchModel.ProductSearchModel.PageSize,
                    forecast.Count()),
                () => forecast);
            return Json(model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> ExportCsv(object model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();
            return Ok();
        }
    }
}
