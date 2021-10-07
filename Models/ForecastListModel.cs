using Nop.Web.Framework.Models;
using Nop.Core.Domain.Catalog;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class ForecastListModel : BasePagedListModel<ForecastResponse>
    {
    }

    public class ForecastResponse : BaseNopEntityModel
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public int Prediction { get; set; }

        public ForecastResponse() {}

        public ForecastResponse(Product product, int prediction)
        {
            ProductId = product.Id.ToString();
            Name = product.Name;
            Sku = product.Sku;
            Prediction = prediction;
        }
    }
}
