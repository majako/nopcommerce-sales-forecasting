using Majako.Plugin.Misc.SalesForecasting.Factories;
using Majako.Plugin.Misc.SalesForecasting.Services;
using Majako.Services.Factories;
using Majako.Services.Helpers;
using Majako.Services.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ISalesForecastingService, SalesForecastingService>();
            services.AddScoped<ISalesForecastModelFactory, SalesForecastModelFactory>();

            var instance = EngineContext.Current.Resolve<ISalesForecastingService>();
            var instance2 = EngineContext.Current.Resolve<ISalesForecastingService>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }
        public int Order => 2505;
    }
}
