using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routes)
        {
            routes.MapRoute("Plugin.Misc.SalesForecasting.Configure",
                 "Admin/SalesForecasting/Configure",
                 new { controller = "SalesForecasting", action = "Configure" }
            );
        }
        public int Priority
        {
            get
            {
                return 100;
            }
        }
    }
}
