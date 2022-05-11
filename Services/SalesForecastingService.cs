using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Core.Data;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Models.Catalog;

namespace Majako.Plugin.Misc.SalesForecasting.Services
{
  public class SalesForecastingService
  {
    private class ForecastRequest
    {
      public IDictionary<string, float> Params { get; set; }
      public int? Period { get; set; }
      public float? MinWeight { get; set; }
      public float[] Quantiles { get; set; }
      public IEnumerable<Sale> Data { get; set; }
      public IDictionary<string, float> Discounts { get; set; }
    }

    private class RawForecastResponse
    {
      public ForecastData Data { get; set; }
    }

    private class Prediction
    {
      public string ProductId { get; set; }
      public int Quantity { get; set; }
      public float MeanError { get; set; }
      public float StandardDeviation { get; set; }
    }

    private class ForecastData
    {
      public IList<Prediction> Predictions { get; set; }
    }

    private class ForecastSubmittedResponse
    {
      public string Id { get; set; }
    }

    private const string BASE_URL = "https://api.majako.net/sales-forecast/v1/";
    private readonly HttpClient _httpClient;
    private readonly ISettingService _settingService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IDiscountService _discountService;
    private readonly IWebHelper _webHelper;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<OrderItem> _orderItemRepository;
    private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
    {
      ContractResolver = new CamelCasePropertyNamesContractResolver(),
      NullValueHandling = NullValueHandling.Ignore,
      StringEscapeHandling = StringEscapeHandling.EscapeHtml
    };
    private CancellationTokenSource _pollingCancellationTokenSource = new CancellationTokenSource();

    public SalesForecastingService(
        IRepository<Order> orderRepository,
        IRepository<OrderItem> orderItemRepository,
        ISettingService settingService,
        IProductService productService,
        INotificationService notificationService,
        ICategoryService categoryService,
        ILocalizationService localizationService,
        IDiscountService discountService,
        IHttpClientFactory httpClientFactory,
        IWebHelper webHelper
    )
    {
      _httpClient = httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
      _orderRepository = orderRepository;
      _settingService = settingService;
      _notificationService = notificationService;
      _productService = productService;
      _categoryService = categoryService;
      _localizationService = localizationService;
      _discountService = discountService;
      _webHelper = webHelper;
      _orderItemRepository = orderItemRepository;
    }

    public async Task SubmitForecastAsync(ForecastSubmissionModel model)
    {
      var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
      var discountsByProduct = new Dictionary<int, int[]>(model.DiscountsByProduct);
      var data = GetData(discountsByProduct.Keys.ToArray());
      if (!data.Any())
        return;
      var request = new ForecastRequest
      {
        Data = data,
        Period = model.PeriodLength,
        Quantiles = settings.Quantile > 0 ? new [] { settings.Quantile / 100f } : Array.Empty<float>(),
        Discounts = model.BlanketDiscount.HasValue
            ? discountsByProduct.ToDictionary(kv => kv.Key.ToString(), kv => model.BlanketDiscount.Value)
            : GetAppliedDiscounts(discountsByProduct, model.PeriodLength)
      };
      using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}forecast"))
      {
        var requestContent = new StringContent(JsonConvert.SerializeObject(request, _jsonSerializerSettings));
        requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        requestMessage.Content = requestContent;
        requestMessage.Headers.Add("subscription-key", settings.ApiKey);
        var response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
        try
        {
          response.EnsureSuccessStatusCode();
        }
        catch
        {
          var messageKey = response.StatusCode == HttpStatusCode.Unauthorized
            ? "Majako.Plugin.Misc.SalesForecasting.InvalidSubscriptionKey"
            : "Majako.Plugin.Misc.SalesForecasting.ForecastFailed";
          _notificationService.ErrorNotification(_localizationService.GetResource(messageKey), encode: false);
          throw;
        }
        var forecastId = JsonConvert.DeserializeObject<ForecastSubmittedResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Id;
        settings.ForecastId = forecastId;
      }
      _settingService.SaveSetting(settings);
      _pollingCancellationTokenSource.Cancel();
      _pollingCancellationTokenSource = new CancellationTokenSource();
      _ = PollForecastAsync(_pollingCancellationTokenSource.Token);
    }

    public PreliminaryForecastModel GetPreliminaryData(ForecastSearchModel searchModel)
    {
      var products = GetProductsFromSearch(searchModel);
      var (fromUtc, untilUtc) = GetPeriod(searchModel.PeriodLength);
      var discounts = GetDiscounts(products, fromUtc, untilUtc);
      var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
      var searchModelJson = JsonConvert.SerializeObject(searchModel, _jsonSerializerSettings);

      settings.SearchModelJsonGzip = Compress(searchModelJson);
      _settingService.SaveSetting(settings);

      return new PreliminaryForecastModel
      {
        DiscountsByProduct = discounts,
        PeriodLength = searchModel.PeriodLength
      };
    }

    public async Task<IEnumerable<ForecastResponse>> GetForecastAsync()
    {
      var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
      if (string.IsNullOrEmpty(settings.ForecastId))
        throw new Exception("No forecast found");

      var response = await GetForecastResponseAsync(settings);
      response.EnsureSuccessStatusCode();
      if (response.StatusCode != HttpStatusCode.OK)
        return null;
      var content = JsonConvert.DeserializeObject<RawForecastResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
      var predictions = content.Data.Predictions.ToDictionary(p => p.ProductId);

      var searchModelJson = Decompress(settings.SearchModelJsonGzip);
      var searchModel = JsonConvert.DeserializeObject<ForecastSearchModel>(searchModelJson);

      return GetProductsFromSearch(searchModel).Select(p =>
      {
        return predictions.TryGetValue(p.Id.ToString(), out var prediction)
          ? new ForecastResponse(
              p,
              prediction.Quantity,
              prediction.MeanError,
              prediction.StandardDeviation,
              prediction.Quantiles)
          : new ForecastResponse(p, 0, 0, null);
      });
    }

    public IEnumerable<Sale> GetData(int[] productIds)
    {
      if (productIds.Length == 0)
        return Enumerable.Empty<Sale>();
      var query =
          from productId in productIds
          join orderItem in _orderItemRepository.Table on productId equals orderItem.ProductId
          join order in _orderRepository.Table on orderItem.OrderId equals order.Id
          where order.OrderStatusId != (int)OrderStatus.Cancelled
          select new
          {
            productId,
            order,  // date cannot be saved directly for some reason
            quantity = orderItem.Quantity,
            discount = orderItem.DiscountAmountExclTax,
            price = orderItem.PriceExclTax
          };
      return query.ToArray().Select(r => new Sale
      {
        ProductId = r.productId.ToString(),
        Created = r.order.CreatedOnUtc,
        Quantity = r.quantity,
        Discount = r.price > 0 ? r.discount / r.price : 0
      });
    }

    public IEnumerable<Sale> GetData(ProductSearchModel productSearchModel)
    {
      return GetData(GetProductsFromSearch(productSearchModel).Select(p => p.Id).ToArray());
    }

    private string Compress(string s)
    {
      using (var input = new MemoryStream(Encoding.Unicode.GetBytes(s)))
      {
        using (var output = new MemoryStream())
        {
          using (var stream = new GZipStream(output, CompressionLevel.Optimal))
          {
            input.CopyTo(stream);
          }
          return Convert.ToBase64String(output.ToArray());
        }
      }
    }

    private string Decompress(string s)
    {
      using (var input = new MemoryStream(Convert.FromBase64String(s)))
      {
        using (var output = new MemoryStream())
        {
          using (var stream = new GZipStream(input, CompressionMode.Decompress))
          {
            stream.CopyTo(output);
          }
          return Encoding.Unicode.GetString(output.ToArray());
        }
      }
    }

    private async Task PollForecastAsync(CancellationToken token)
    {
      var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
      while (!token.IsCancellationRequested)
      {
        try
        {
          var response = await GetForecastResponseAsync(settings);
          response.EnsureSuccessStatusCode();
          if (response.StatusCode == HttpStatusCode.OK)
          {
            var url = $"{_webHelper.GetStoreLocation()}{SalesForecastingPlugin.BASE_ROUTE}/{SalesForecastingPlugin.FORECAST}";
            _notificationService.SuccessNotification(
              _localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastReady") +
                $" <a href=\"{url}\">{_localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastLinkText")}</a>",
              encode: false
            );
            break;
          }
        }
        catch
        {
          _notificationService.ErrorNotification(_localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastFailed"));
          break;
        }
        await Task.Delay(5000, token);
      }
    }

    private async Task<HttpResponseMessage> GetForecastResponseAsync(SalesForecastingPluginSettings settings)
    {
      using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{BASE_URL}forecast/{settings.ForecastId}"))
      {
        requestMessage.Headers.Add("subscription-key", settings.ApiKey);
        return await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
      }
    }

    private IDictionary<int, Discount[]> GetDiscounts(IEnumerable<Product> products, DateTime fromUtc, DateTime untilUtc)
    {
      var discounts = _discountService
        .GetAllDiscounts(showHidden: true)
        .Where(d => !d.StartDateUtc.HasValue || d.StartDateUtc <= untilUtc)
        .Where(d => !d.EndDateUtc.HasValue || d.EndDateUtc >= fromUtc)
        .ToArray();

      var discountsByProduct = products.ToDictionary(
        p => p.Id,
        _ => (IList<Discount>)new List<Discount>()
      );

      void add(int key, Discount value) => discountsByProduct.GetValueOrDefault(key)?.Add(value);

      var productsById = products.ToDictionary(p => p.Id);
      foreach (var dpm in discounts.SelectMany(d => d.DiscountProductMappings.Where(x => productsById.ContainsKey(x.ProductId))))
        add(dpm.ProductId, dpm.Discount);

      var productsByManufacturer = products
        .SelectMany(p => p.ProductManufacturers.Select(m => (mid: m.ManufacturerId, pid: p.Id)))
        .ToLookup(x => x.mid, x => x.pid);
      foreach (var dmm in discounts.SelectMany(d => d.DiscountManufacturerMappings))
      {
        foreach (var grouping in productsByManufacturer[dmm.ManufacturerId])
          add(grouping, dmm.Discount);
      }

      foreach (var dcm in discounts.SelectMany(d => d.DiscountCategoryMappings))
      {
        var applicableCategoryIds = new List<int> { dcm.CategoryId };
        if (dcm.Discount.AppliedToSubCategories)
        {
          applicableCategoryIds.AddRange(_categoryService
            .GetAllCategoriesByParentCategoryId(dcm.CategoryId, showHidden: true)
            .Select(c => c.Id)
          );
        }
        foreach (var pc in applicableCategoryIds.SelectMany(cid => _categoryService.GetProductCategoriesByCategoryId(cid, showHidden: true)))
          add(pc.ProductId, dcm.Discount);
      }

      var orderDiscounts = discounts
        .Where(d => d.DiscountType == DiscountType.AssignedToOrderTotal || d.DiscountType == DiscountType.AssignedToOrderSubTotal)
        .ToArray();

      return discountsByProduct.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Concat(orderDiscounts).ToArray()
      );
    }

    private IDictionary<string, float> GetAppliedDiscounts(IDictionary<int, int[]> discountsByProduct, int periodLength)
    {
      var productsById = new Dictionary<int, Product>(discountsByProduct.Count);
      var batchSize = 1000;
      for (var offset = 0; offset < discountsByProduct.Count; offset += batchSize)
      {
        var batch = discountsByProduct.Keys.Skip(offset).Take(batchSize).ToArray();
        foreach (var product in _productService.GetProductsByIds(batch))
          productsById.Add(product.Id, product);
      }
      var discountsById = _discountService
        .GetAllDiscounts(showHidden: true)
        .ToDictionary(d => d.Id);
      var (fromUtc, untilUtc) = GetPeriod(periodLength);
      float coverage(DiscountForCaching discount)
      {
        var startDiff = discount.StartDateUtc.HasValue
          ? Math.Max((discount.StartDateUtc.Value - fromUtc).TotalDays, 0)
          : 0;
        var endDiff = discount.EndDateUtc.HasValue
          ? Math.Max((untilUtc - discount.EndDateUtc.Value).TotalDays, 0)
          : 0;
        return 1 - (float)(startDiff + endDiff) / periodLength;
      }
      return discountsByProduct
        .Select(kv =>
        {
          var price = productsById.GetValueOrDefault(kv.Key)?.Price ?? 0;
          if (price == 0)
            return (pid: kv.Key, discount: 0m);
          var appliedDiscounts = _discountService.GetPreferredDiscount(
                  kv.Value.Select(d => _discountService.MapDiscount(discountsById[d])).ToList(),
                  price,
                  out var discountAmount
                );
          var avgCoverage = appliedDiscounts.Count > 0 ? (decimal)appliedDiscounts.Select(coverage).Average() : 0;
          return (pid: kv.Key, discount: avgCoverage * discountAmount / price);
        })
        .Where(t => t.discount != 0)
        .ToDictionary(
          t => t.pid.ToString(),
          t => (float)t.discount
        );
    }

    private IList<Product> GetProductsFromSearch(ProductSearchModel productSearchModel)
    {
      var categoryIds = new List<int>();
      if (productSearchModel.SearchCategoryId > 0)
      {
        categoryIds.Add(productSearchModel.SearchCategoryId);

        if (productSearchModel.SearchIncludeSubCategories)
        {
          var childCategoryIds = _categoryService.GetChildCategoryIds(parentCategoryId: productSearchModel.SearchCategoryId, showHidden: false);
          categoryIds.AddRange(childCategoryIds);
        }
      }

      bool? overridePublished = null;
      if (productSearchModel.SearchPublishedId > 0)
      {
        if (productSearchModel.SearchPublishedId == 1)
          overridePublished = true;
        else if (productSearchModel.SearchPublishedId == 2)
          overridePublished = false;
      }

      return _productService.SearchProducts(
          showHidden: true,
          categoryIds: categoryIds,
          manufacturerId: productSearchModel.SearchManufacturerId,
          storeId: productSearchModel.SearchStoreId,
          productType: productSearchModel.SearchProductTypeId > 0 ? (ProductType?)productSearchModel.SearchProductTypeId : null,
          keywords: productSearchModel.SearchProductName,
          pageIndex: 0, pageSize: int.MaxValue,
          overridePublished: overridePublished);
    }

    private static (DateTime fromUtc, DateTime untilUtc) GetPeriod(int periodLength)
    {
      var startDate = DateTime.Today.AddDays(1).ToUniversalTime();
      return (startDate, startDate + TimeSpan.FromDays(periodLength));
    }
  }
}
