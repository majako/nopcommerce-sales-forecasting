using System;
using System.Threading.Tasks;
using System.Linq;
using Majako.Plugin.Misc.SalesForecasting;
using Majako.Plugin.Misc.SalesForecasting.Services;
using Microsoft.AspNetCore.Mvc;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

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

            return View("~/Plugins/Misc.SalesForecasting/Views/Forecast.cshtml", null);
        }

        [HttpPost, ActionName("Configure")]
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

        [HttpPost, ActionName("PostForecast")]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> PostForecastAsync(ProductSearchModel productSearchModel, int period)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageOrders))
                return AccessDeniedView();
            var forecast = await _salesForecastingService.ForecastAsync(period, productSearchModel);
            return Ok(forecast);
        }
    }
}
