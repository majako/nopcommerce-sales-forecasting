using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Areas.Admin.Factories;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Models;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastSearchModel
    {
        public int PeriodLength { get; set; }
        public ProductSearchModel ProductSearchModel { get; set; }

        public ForecastSearchModel()
        {
            PeriodLength = 14;
            ProductSearchModel = EngineContext.Current.Resolve<IProductModelFactory>().PrepareProductSearchModel(new ProductSearchModel());
        }
    }
}
