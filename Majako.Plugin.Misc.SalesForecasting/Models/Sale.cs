using System;

namespace Majako.Plugin.Misc.SalesForecasting.Models
{
    public class Sale
    {
      public string ProductId { get; set; }
      public DateTime Created { get; set; }
      public int Quantity { get; set; }
      public decimal Discount { get; set; }
    }
}
