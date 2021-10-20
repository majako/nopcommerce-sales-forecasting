﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute("Plugin.Misc.SalesForecasting.Admin.Configure",
                "Admin/SalesForecasting/Configure",
                new { controller = "SalesForecasting", action = "Configure" }
            );

            endpointRouteBuilder.MapControllerRoute("Plugin.Misc.SalesForecasting.Admin.Forecast",
                 "Admin/SalesForecasting/Forecast",
                 new { controller = "SalesForecasting", action = "Forecast" }
            );
        }

        public int Priority => -1;
    }
}
