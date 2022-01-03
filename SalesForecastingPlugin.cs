using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Services.Common;
using Nop.Services.Plugins;
using Nop.Services.Tasks;
using NUglify.Helpers;
using Nop.Web.Framework.Menu;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public class SalesForecastingPlugin : BasePlugin, IMiscPlugin, IAdminMenuPlugin
    {
        public const string SYSTEM_NAME = "Misc.SalesForecasting";
        public const string BASE_ROUTE = "Admin/SalesForecasting";
        public const string CONFIGURE = "Configure";
        public const string FORECAST = "Forecast";

        private readonly IWebHelper _webHelper;

        public SalesForecastingPlugin(
            IWebHelper webHelper)
        {
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}{BASE_ROUTE}/{CONFIGURE}";
        }

        public void ManageSiteMap(SiteMapNode rootNode)
        {
            var salesNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Sales");
            if (salesNode == null) return;
            salesNode.ChildNodes.Insert(salesNode.ChildNodes.Count, new SiteMapNode
            {
                // Title = _localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting"),
                Title = "Försäljningsprognoser",
                Url = $"/{BASE_ROUTE}/{FORECAST}",
                Visible = true,
                RouteValues = new RouteValueDictionary { { "Area", "Admin" } },
                IconClass = "fa-dot-circle-o",
                SystemName = "Order.SalesForecasting"
            });
        }

        public override void Install()
        {
            base.Install();
        }

        public override void Uninstall()
        {
            base.Uninstall();
        }
    }
}
