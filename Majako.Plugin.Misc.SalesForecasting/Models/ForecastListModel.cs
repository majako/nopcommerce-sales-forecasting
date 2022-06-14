using Nop.Web.Framework.Models;
using Nop.Core.Domain.Catalog;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
  public record ForecastListModel : BasePagedListModel<ForecastResponse>
  {
  }

  public record ForecastResponse : BaseNopEntityModel
  {
    public string ProductId { get; set; }
    public string Name { get; set; }
    public string Sku { get; set; }
    public int Prediction { get; set; }
    public int QuantilePrediction { get; set; }

    public ForecastResponse() { }

    public ForecastResponse(Product product, int prediction, int[] quantiles)
    {
      ProductId = product.Id.ToString();
      Name = product.Name;
      Sku = product.Sku;
      Prediction = prediction;
      QuantilePrediction = quantiles?.Length == 1 ? quantiles[0] : prediction;
    }
  }
}
