using Nop.Core.Configuration;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Majako.Plugin.Misc.SalesForecasting
{
    public class SalesForecastingPluginSettings : ISettings
    {
        [NopResourceDisplayName("Majako.Plugin.Misc.SalesForecasting.ApiKey")]
        public string ApiKey { get; set; }

        public string ForecastId { get; set; }
        public string SearchModelJson { get; set; }
    }
}
