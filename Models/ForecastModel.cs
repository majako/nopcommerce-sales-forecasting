namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastModel
    {
        public int PeriodLength { get; set; }

        public ForecastModel()
        {
            PeriodLength = 14;
        }
    }
}
