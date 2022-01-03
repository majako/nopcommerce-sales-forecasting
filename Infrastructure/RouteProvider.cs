using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.MapControllerRoute("Plugin.Misc.SalesForecasting.Admin.Configure",
                $"{SalesForecastingPlugin.BASE_ROUTE}/{SalesForecastingPlugin.CONFIGURE}",
                new { controller = "SalesForecasting", action = "Configure" }
            );

            routeBuilder.MapControllerRoute("Plugin.Misc.SalesForecasting.Admin.Forecast",
                 $"{SalesForecastingPlugin.BASE_ROUTE}/{SalesForecastingPlugin.FORECAST}",
                 new { controller = "SalesForecasting", action = "Forecast" }
            );
        }

        public int Priority => -1;
    }
}
