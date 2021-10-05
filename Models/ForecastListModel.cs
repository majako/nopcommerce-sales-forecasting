using Nop.Web.Framework.Models;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public partial class ForecastListModel : BasePagedListModel<ForecastResponse>
    {
    }

    public class ForecastResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public int Prediction { get; set; }

        public ForecastResponse(Product product, int prediction)
        {
            Id = product.Id.ToString();
            Name = product.Name;
            Sku = product.Sku;
            Prediction = prediction;
        }
    }
}
