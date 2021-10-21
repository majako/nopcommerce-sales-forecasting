using Autofac;
using Majako.Plugin.Misc.SalesForecasting.Services;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            // services
            services.AddScoped<SalesForecastingService>();
        }

        public int Order => 1;
    }
}
