using Nop.Web.Framework.Models;
using Nop.Core.Domain.Catalog;
using System.Collections.Generic;
using Nop.Core.Domain.Discounts;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class PreliminaryForecastModel : BaseNopEntityModel
    {
        public int[] ProductIds { get; set; }
        public IDictionary<int, IList<Discount>> Discounts { get; set; }
        public int PeriodLength { get; set; }

        public PreliminaryForecastModel() {}
    }
}
