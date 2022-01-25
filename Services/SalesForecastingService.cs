using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Nop.Data;
using Nop.Core.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Models.Catalog;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Nop.Core;
using Nop.Services.Discounts;
using Nop.Core.Domain.Discounts;

namespace Majako.Plugin.Misc.SalesForecasting.Services
{
  public class SalesForecastingService
  {
    private class Sale
    {
      public string ProductId { get; set; }
      public DateTime Created { get; set; }
      public int Quantity { get; set; }
      public decimal Discount { get; set; }
    }

    private class ForecastRequest
    {
      public IDictionary<string, float> Params { get; set; }
      public int? Period { get; set; }
      public float? MinWeight { get; set; }
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
    private readonly IManufacturerService _manufacturerService;
    private readonly IWebHelper _webHelper;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<OrderItem> _orderItemRepository;
    private readonly IRepository<DiscountProductMapping> _discountProductMappingRepository;
    private readonly IRepository<DiscountCategoryMapping> _discountCategoryMappingRepository;
    private readonly IRepository<DiscountManufacturerMapping> _discountManufacturerMappingRepository;
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
        IRepository<DiscountProductMapping> discountProductMappingRepository,
        IRepository<DiscountCategoryMapping> discountCategoryMappingRepository,
        IRepository<DiscountManufacturerMapping> discountManufacturerMappingRepository,
        ISettingService settingService,
        IProductService productService,
        INotificationService notificationService,
        ICategoryService categoryService,
        ILocalizationService localizationService,
        IDiscountService discountService,
        IManufacturerService manufacturerService,
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
      _manufacturerService = manufacturerService;
      _webHelper = webHelper;
      _orderItemRepository = orderItemRepository;
      _discountProductMappingRepository = discountProductMappingRepository;
      _discountCategoryMappingRepository = discountCategoryMappingRepository;
      _discountManufacturerMappingRepository = discountManufacturerMappingRepository;
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
        Discounts = model.BlanketDiscount > 0 && model.BlanketDiscount <= 1
            ? discountsByProduct.ToDictionary(kv => kv.Key.ToString(), kv => model.BlanketDiscount)
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
      settings.SearchModelJson = JsonConvert.SerializeObject(model, _jsonSerializerSettings);
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

      var response = await GetForecastResponse(settings);
      response.EnsureSuccessStatusCode();
      if (response.StatusCode != HttpStatusCode.OK)
        return null;
      var content = JsonConvert.DeserializeObject<RawForecastResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
      var predictions = content.Data.Predictions.ToDictionary(p => p.ProductId, p => p.Quantity);
      var searchModel = JsonConvert.DeserializeObject<ForecastSearchModel>(settings.SearchModelJson);
      return GetProductsFromSearch(searchModel).Select(p => new ForecastResponse(
        p,
        predictions.TryGetValue(p.Id.ToString(), out var prediction) ? prediction : 0));
    }

    private async Task PollForecastAsync(CancellationToken token)
    {
      var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
      while (!token.IsCancellationRequested)
      {
        try
        {
          var response = await GetForecastResponse(settings);
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

    private async Task<HttpResponseMessage> GetForecastResponse(SalesForecastingPluginSettings settings)
    {
      using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{BASE_URL}forecast/{settings.ForecastId}");
      requestMessage.Headers.Add("subscription-key", settings.ApiKey);
      return await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
    }

    private IEnumerable<Sale> GetData(int[] productIds)
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

    private IDictionary<int, Discount[]> GetDiscounts(IEnumerable<Product> products, DateTime fromUtc, DateTime untilUtc)
    {
      var discountsByProduct = products.ToDictionary(
        p => p.Id,
        _ => (IList<Discount>)new List<Discount>()
      );

      var discountsById = _discountService
        .GetAllDiscounts(showHidden: true)
        .Where(d => !d.StartDateUtc.HasValue || d.StartDateUtc <= untilUtc)
        .Where(d => !d.EndDateUtc.HasValue || d.EndDateUtc >= fromUtc)
        .ToDictionary(d => d.Id);

      void add(int productId, int discountId)
      {
        if (discountsById.TryGetValue(discountId, out var discount))
          discountsByProduct.GetValueOrDefault(productId)?.Add(discount);
      }
      int[] getDiscountIdsByType(DiscountType discountType) =>
        discountsById.Values
        .Where(d => d.DiscountType == discountType)
        .Select(d => d.Id)
        .ToArray();

      // SQL server has a max limit of elements in an IN query, so we need to filter locally
      var discountedProductIds = products
        .Where(p => p.HasDiscountsApplied)
        .Select(p => p.Id)
        .ToHashSet();
      var discountProductMappings =
        from dpm in _discountProductMappingRepository.Table
        join discountId in getDiscountIdsByType(DiscountType.AssignedToSkus)
          on dpm.DiscountId equals discountId
        select new { pid = dpm.EntityId, discountId };

      foreach (var dpm in discountProductMappings.ToArray().Where(x => discountedProductIds.Contains(x.pid)))
        add(dpm.pid, dpm.discountId);

      var discountManufacturerMappings =
        (from dmm in _discountManufacturerMappingRepository.Table
         join discountId in getDiscountIdsByType(DiscountType.AssignedToManufacturers)
           on dmm.DiscountId equals discountId
         select new { mid = dmm.EntityId, discountId })
        .ToArray();
      foreach (var dmm in discountManufacturerMappings)
      {
        foreach (var pm in _manufacturerService.GetProductManufacturersByManufacturerId(dmm.mid, showHidden: true))
          add(pm.ProductId, dmm.discountId);
      }

      var discountCategoryMappings =
        (from dcm in _discountCategoryMappingRepository.Table
         join discountId in getDiscountIdsByType(DiscountType.AssignedToCategories)
           on dcm.DiscountId equals discountId
         select new { cid = dcm.EntityId, discountId })
        .ToArray();
      foreach (var dcm in discountCategoryMappings)
      {
        var applicableCategoryIds = new List<int> { dcm.cid };
        if (discountsById.TryGetValue(dcm.discountId, out var discount) && discount.AppliedToSubCategories)
        {
          applicableCategoryIds.AddRange(_categoryService
            .GetAllCategoriesByParentCategoryId(dcm.cid, showHidden: true)
            .Select(c => c.Id)
          );
        }
        foreach (var pc in applicableCategoryIds.SelectMany(cid => _categoryService.GetProductCategoriesByCategoryId(cid, showHidden: true)))
          add(pc.ProductId, dcm.discountId);
      }

      var orderDiscounts = discountsById.Values
        .Where(d => d.DiscountType == DiscountType.AssignedToOrderTotal || d.DiscountType == DiscountType.AssignedToOrderSubTotal)
        .ToArray();

      return discountsByProduct.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Concat(orderDiscounts).ToArray()
      );
    }

    private IDictionary<string, float> GetAppliedDiscounts(IDictionary<int, int[]> discountsByProduct, int periodLength)
    {
      var productsById = _productService
        .GetProductsByIds(discountsByProduct.Keys.ToArray())
        .ToDictionary(p => p.Id);
      var discountsById = _discountService
        .GetAllDiscounts(showHidden: true)
        .ToDictionary(d => d.Id);
      var (fromUtc, untilUtc) = GetPeriod(periodLength);
      float coverage(Discount discount)
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
          var price = productsById[kv.Key].Price;
          if (price == 0)
            return (pid: kv.Key, discount: 0m);
          var appliedDiscounts = _discountService.GetPreferredDiscount(
                  kv.Value.Select(discountsById.GetValueOrDefault).ToList(),
                  price,
                  out var discountAmount
                );
          var avgCoverage = (decimal)appliedDiscounts.Select(coverage).Average();
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
