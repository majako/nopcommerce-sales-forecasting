using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Majako.Plugin.Misc.SalesForecasting.Services;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<SalesForecastingService>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }
        public int Order => 1;
    }
}
