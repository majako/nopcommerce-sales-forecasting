using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Nop.Core.Data;
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

        private const string BASE_URL = "https://majako-sales-forecasting.azurewebsites.net/";
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
                Discounts = GetAppliedDiscounts(discountsByProduct)
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(request, _jsonSerializerSettings));
            requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            var response = await _httpClient.PostAsync($"{BASE_URL}forecast", requestContent).ConfigureAwait(false);
            var forecastId = JsonConvert.DeserializeObject<ForecastSubmittedResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Id;
            settings.ForecastId = forecastId;
            settings.SearchModelJson = JsonConvert.SerializeObject(model, _jsonSerializerSettings);
            _settingService.SaveSetting(settings);
            _pollingCancellationTokenSource.Cancel();
            _pollingCancellationTokenSource = new CancellationTokenSource();
            _ = PollForecastAsync(_pollingCancellationTokenSource.Token);
        }

        public PreliminaryForecastModel GetPreliminaryData(ForecastSearchModel searchModel)
        {
            var products = GetProductsFromSearch(searchModel);
            var startDate = DateTime.Today;
            var discounts = GetDiscounts(products, startDate, startDate + TimeSpan.FromDays(searchModel.PeriodLength));
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
            var getForecastResponse = await _httpClient.GetAsync($"{BASE_URL}forecast/{settings.ForecastId}").ConfigureAwait(false);
            getForecastResponse.EnsureSuccessStatusCode();
            if (getForecastResponse.StatusCode != HttpStatusCode.OK)
                throw new Exception("Forecast not ready");
            var content = JsonConvert.DeserializeObject<RawForecastResponse>(await getForecastResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
            var predictions = content.Data.Predictions.ToDictionary(p => p.ProductId, p => p.Quantity);
            var searchModel = JsonConvert.DeserializeObject<ForecastSearchModel>(settings.SearchModelJson);
            return GetProductsFromSearch(searchModel).Select(p => new ForecastResponse(
              p,
              predictions.TryGetValue(p.Id.ToString(), out var prediction) ? prediction : 0));
        }

        private async Task PollForecastAsync(CancellationToken token)
        {
            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            var forecastId = settings.ForecastId;
            while (!token.IsCancellationRequested)
            {
                var getForecastResponse = await _httpClient.GetAsync($"{BASE_URL}forecast/{forecastId}").ConfigureAwait(false);
                try
                {
                    getForecastResponse.EnsureSuccessStatusCode();
                }
                catch
                {
                    _notificationService.ErrorNotification(_localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastFailed"));
                    break;
                }
                if (getForecastResponse.StatusCode == HttpStatusCode.OK)
                {
                    var content = JsonConvert.DeserializeObject<RawForecastResponse>(await getForecastResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
                    var predictions = content.Data.Predictions.ToDictionary(p => p.ProductId, p => p.Quantity);
                    var url = $"{_webHelper.GetStoreLocation()}{SalesForecastingPlugin.BASE_ROUTE}/{SalesForecastingPlugin.FORECAST}";
                    _notificationService.SuccessNotification(
                      _localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastReady") +
                        $" <a href=\"{url}\">{_localizationService.GetResource("Majako.Plugin.Misc.SalesForecasting.ForecastLinkText")}</a>",
                      encode: false
                    );
                    break;
                }
                await Task.Delay(5000);
            }
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
                    productId = productId,
                    order = order,  // date cannot be saved directly for some reason
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

        private IDictionary<int, Discount[]> GetDiscounts(IEnumerable<Product> products, DateTime from, DateTime until)
        {
            var discounts = _discountService
              .GetAllDiscounts(
                showHidden: true,
                startDateUtc: from.ToUniversalTime(),
                endDateUtc: until.ToUniversalTime()
              );

            var discountsByProduct = new Dictionary<int, IList<Discount>>();
            void add(int key, Discount value)
            {
                discountsByProduct.GetValueOrDefault(
                  key,
                  discountsByProduct[key] = new List<Discount>()
                ).Add(value);
            }

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

            var productsByCategory = _categoryService
              .GetProductCategoryIds(productsById.Keys.ToArray())
              .SelectMany(kv => kv.Value.Select(cid => (cid, pid: kv.Key)))
              .ToLookup(x => x.cid, x => x.pid);
            foreach (var dcm in discounts.SelectMany(d => d.DiscountCategoryMappings))
            {
                foreach (var grouping in productsByCategory[dcm.CategoryId])
                    add(grouping, dcm.Discount);
            }

            return discountsByProduct.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        }

        private IDictionary<string, float> GetAppliedDiscounts(IDictionary<int, int[]> discountsByProduct)
        {
            var productsById = _productService
              .GetProductsByIds(discountsByProduct.Keys.ToArray())
              .ToDictionary(p => p.Id);
            var discountsById = _discountService
              .GetAllDiscounts(showHidden: true)
              .ToDictionary(d => d.Id);
            return discountsByProduct
              .Select(kv =>
              {
                  var price = productsById[kv.Key].Price;
                  if (price == 0)
                      return (pid: kv.Key, discount: 0m);
                  _discountService.GetPreferredDiscount(
                    kv.Value.Select(d => _discountService.MapDiscount(discountsById[d])).ToList(),
                    price,
                    out var discountAmount
                  );
                  return (pid: kv.Key, discount: discountAmount / price);
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
    }
}
