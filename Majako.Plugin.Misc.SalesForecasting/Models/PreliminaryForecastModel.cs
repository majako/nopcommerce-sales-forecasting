using Nop.Web.Framework.Models;
using Nop.Core.Domain.Catalog;
using System.Collections.Generic;
using Nop.Core.Domain.Discounts;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class PreliminaryForecastModel
    {
        public IDictionary<int, Discount[]> DiscountsByProduct { get; set; }
        public int PeriodLength { get; set; }
    }
}
