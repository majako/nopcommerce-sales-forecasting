using Nop.Web.Framework.Models;
using Nop.Core.Domain.Catalog;
using System.Collections.Generic;
using Nop.Core.Domain.Discounts;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class PreliminaryForecastModel : BaseNopEntityModel
    {
        public IEnumerable<int> ProductIds { get; set; }
        public IEnumerable<Discount> Discounts { get; set; }

        public PreliminaryForecastModel() {}
    }
}
