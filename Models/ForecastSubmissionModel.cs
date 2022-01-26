using System.Collections.Generic;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
  public class ForecastSubmissionModel
    {
        public IEnumerable<KeyValuePair<int, int[]>> DiscountsByProduct { get; set; }
        public float? BlanketDiscount { get; set; }
        public int PeriodLength { get; set; }
    }
}
