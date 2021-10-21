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
using Task = System.Threading.Tasks.Task;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public class SalesForecastingPlugin : BasePlugin, IMiscPlugin, IAdminMenuPlugin
    {
        public const string SystemName = "Misc.SalesForecasting";

        private readonly IWebHelper _webHelper;

        public SalesForecastingPlugin(
            IWebHelper webHelper)
        {
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/SalesForecasting/Configure";
        }

        public async Task ManageSiteMapAsync(SiteMapNode rootNode)
        {
            var salesNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Sales");
            if (salesNode == null) return;
            salesNode.ChildNodes.Insert(salesNode.ChildNodes.Count, new SiteMapNode
            {
                // Title = _localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting"),
                Title = "Försäljningsprognoser",
                Url = "/Admin/SalesForecasting/Forecast",
                Visible = true,
                RouteValues = new RouteValueDictionary { { "Area", "Admin" } },
                IconClass = "fa-dot-circle-o",
                SystemName = "Order.SalesForecasting"
            });
        }
    }
}
