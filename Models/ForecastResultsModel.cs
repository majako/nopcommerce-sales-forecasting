using System.Linq;
using System.Collections.Generic;
using Nop.Core.Domain.Catalog;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastResultsModel
    {
        public IEnumerable<ForecastResponse> Results { get; set; }
        public int PageSize { get; set; }

        public ForecastResultsModel()
        {
            Results = Enumerable.Empty<ForecastResponse>();
        }
    }
}
