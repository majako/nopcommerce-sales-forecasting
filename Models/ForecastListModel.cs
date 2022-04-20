using System;
using Nop.Core.Domain.Catalog;
using Nop.Web.Framework.Models;

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
    public int UpperPrediction { get; set; }

    public ForecastResponse() { }

    public ForecastResponse(Product product, int prediction, float meanError, float std, float a = 1)
    {
      ProductId = product.Id.ToString();
      Name = product.Name;
      Sku = product.Sku;
      Prediction = prediction;
      UpperPrediction = (int)MathF.Round(prediction - meanError + a * std);
    }
  }
}
