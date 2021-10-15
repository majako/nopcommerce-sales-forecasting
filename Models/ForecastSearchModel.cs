using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Areas.Admin.Factories;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastSearchModel : ProductSearchModel
    {
        [NopResourceDisplayName("Majako.Plugin.Misc.SalesForecasting.PeriodLength")]
        public int PeriodLength { get; set; }

        public ForecastSearchModel()
        {
            PeriodLength = 14;
            EngineContext.Current.Resolve<IProductModelFactory>().PrepareProductSearchModel(this);
        }
    }
}
