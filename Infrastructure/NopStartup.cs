using Majako.Plugin.Common.Extensions;
using Majako.Plugin.Misc.SalesForecasting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Localization;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Majako.Plugin.Misc.SalesForecasting.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void Configure(IApplicationBuilder application)
        {
            var localizationService = EngineContext.Current.Resolve<ILocalizationService>();
            localizationService.InstallXmlLocaleResourcesAsync("sv-se", SalesForecastingPlugin.SystemName).ConfigureAwait(false);
        }

        public int Order => 2000;
    }
}
