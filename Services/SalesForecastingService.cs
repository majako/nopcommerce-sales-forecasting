using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Majako.Services.Extensions;
using Majako.Plugin.Misc.SalesForecasting;
using Nop.Core.Data;
using Nop.Core.Domain.Catalog;
using Nop.Core.Http;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
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

        private class RawForecastResponse
        {
            public IDictionary<string, float> BestParams { get; set; }
            public float BestScore { get; set; }
            public IDictionary<string, int> Predictions { get; set; }
        }

        private const string BASE_URL = "https://majako-sales-forecasting.azurewebsites.net/";
        private readonly HttpClient _httpClient;
        private readonly ISettingService _settingService;
        private readonly IRepository<Order> _orderRepository;
        private readonly IRepository<OrderItem> _orderItemRepository;
        private JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml
        };

        public SalesForecastingService(
            IRepository<Order> orderRepository,
            IRepository<OrderItem> orderItemRepository,
            ISettingService settingService,
            IHttpClientFactory httpClientFactory
        ){
            _httpClient = httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _orderRepository = orderRepository;
            _settingService = settingService;
            _orderItemRepository = orderItemRepository;
        }

        public async Task<ForecastResponse> ForecastAsync(int periodLength, IEnumerable<Product> products, DateTime? until = null)
        {
            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();

            var data = GetData(products);
            if (until != null)
                data = data.Where(s => s.Created <= until);
            if (!data.Any())
                return null;
            var request = new ForecastRequest
            {
                Data = data,
                Period = periodLength
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(request, jsonSerializerSettings));
            requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            var response = await _httpClient.PostAsync($"{BASE_URL}forecast", requestContent).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rawResponse = JsonConvert.DeserializeObject<RawForecastResponse>(responseContent);
            var predictions = new Dictionary<string, IDictionary<string, int>>();
            foreach (var item in rawResponse.Predictions)
            {
                var splitKey = item.Key.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (splitKey.Length == 1)
                    predictions[splitKey[0]] = new Dictionary<string, int> { {string.Empty, item.Value} };
                else if (predictions.TryGetValue(splitKey[0], out var values))
                    values[splitKey[1]] = item.Value;
                else
                    predictions[splitKey[0]] = new Dictionary<string, int> { {splitKey[1], item.Value} };
            }
            return new ForecastResponse
            {
                BestParams = rawResponse.BestParams,
                BestScore = rawResponse.BestScore,
                Predictions = predictions
            };
        }

        private IEnumerable<Sale> GetData(IEnumerable<Product> products)
        {
            var productsById = products.ToDictionary(p => p.Id);
            var productIds = productsById.Keys.ToArray();
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
                    attributes = orderItem.AttributesXml,
                    order = order,  // date cannot be saved directly for some reason
                    quantity = orderItem.Quantity
                };
            return query.ToArray().Select(r => {
                var compositeId = r.productId.ToString();
                if (!productsById[r.productId].HasDyeColorAttribute())
                    compositeId += $" {r.attributes}";
                return new Sale
                {
                    ProductId = compositeId,
                    Created = r.order.CreatedOnUtc,
                    Quantity = r.quantity
                };
            });
        }
    }

    public class ForecastResponse
    {
        public IDictionary<string, float> BestParams { get; set; }
        public float BestScore { get; set; }
        public IDictionary<string, IDictionary<string, int>> Predictions { get; set; }
    }
}
