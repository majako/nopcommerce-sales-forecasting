using Nop.Web.Framework.Models;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public record ForecastResultModel : BaseSearchModel
    {
        public string ResultsJson { get; set; }

        public ForecastResultModel()
        {
            ResultsJson = "[]";
        }
    }
}
