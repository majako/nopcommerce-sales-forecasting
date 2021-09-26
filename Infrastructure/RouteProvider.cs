using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Majako.Plugin.Misc.ChannableApi.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routes)
        {
            routes.MapRoute("Plugin.Misc.ChannableApi.Configure",
                 "Admin/ChannableApi/Configure",
                 new { controller = "ChannableApi", action = "Configure" }
            );
            routes.MapRoute("Plugin.Misc.ChannableApi.GetFeed",
                 "api/feed",
                 new { controller = "ChannableApi", action = "GetFeed" }
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
