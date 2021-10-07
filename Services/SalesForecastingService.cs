using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Majako.Plugin.Misc.SalesForecasting.Models;
using Nop.Core.Data;
using Nop.Core.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Web.Areas.Admin.Models.Catalog;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Majako.Plugin.Misc.SalesForecasting.Services
{
    public class SalesForecastingService
    {
        private class Sale
        {
            public string ProductId { get; set; }
            public DateTime Created { get; set; }
            public int Quantity { get; set; }
        }

        private class ForecastRequest
        {
            public Dictionary<string, float> Params { get; set; }
            public int? Period { get; set; }
            public float? MinWeight { get; set; }
            public IEnumerable<Sale> Data { get; set; }
        }

        public class RawForecastResponse
        {
            public IDictionary<string, float> BestParams { get; set; }
            public float BestScore { get; set; }
            public IDictionary<string, int> Predictions { get; set; }
        }

        private const string BASE_URL = "https://majako-sales-forecasting.azurewebsites.net/";
        private readonly HttpClient _httpClient;
        private readonly ISettingService _settingService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IRepository<Order> _orderRepository;
        private readonly IRepository<OrderItem> _orderItemRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

        public SalesForecastingService(
            IRepository<Order> orderRepository,
            IRepository<OrderItem> orderItemRepository,
            ISettingService settingService,
            IProductService productService,
            ICategoryService categoryService,
            IHttpClientFactory httpClientFactory
        ){
            _httpClient = httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _orderRepository = orderRepository;
            _settingService = settingService;
            _productService = productService;
            _categoryService = categoryService;
            _orderItemRepository = orderItemRepository;
        }

        public async Task<IEnumerable<ForecastResponse>> ForecastAsync(ForecastSearchModel productSearchModel, DateTime? until = null)
        {
            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();
            var products = GetProductsFromSearch(productSearchModel);
            var data = GetData(products.Select(p => p.Id).ToArray());
            if (until != null)
                data = data.Where(s => s.Created <= until);
            if (!data.Any())
                return null;
            var request = new ForecastRequest
            {
                Data = data,
                Period = productSearchModel.PeriodLength
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(request, _jsonSerializerSettings));
            requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            var response = await _httpClient.PostAsync($"{BASE_URL}forecast", requestContent).ConfigureAwait(false);
            var content = JsonConvert.DeserializeObject<RawForecastResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            return products.Select(p => new ForecastResponse(p, content.Predictions.TryGetValue(p.Id.ToString(), out var prediction) ? prediction : 0));
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
                    quantity = orderItem.Quantity
                };
            return query.ToArray().Select(r => new Sale
            {
                ProductId = r.productId.ToString(),
                Created = r.order.CreatedOnUtc,
                Quantity = r.quantity
            });
        }

        private IEnumerable<Product> GetProductsFromSearch(ProductSearchModel productSearchModel)
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
