using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Nop.Core.Data;
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

        private const string BASE_URL = "https://majako-sales-forecasting.azurewebsites.net/";
        private readonly HttpClient _httpClient;
        private readonly ISettingService _settingService;
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
            IHttpClientFactory httpClientFactory
        ){
            _httpClient = httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _orderRepository = orderRepository;
            _settingService = settingService;
            _orderItemRepository = orderItemRepository;
        }

        public async Task<ForecastResponse> ForecastAsync(int periodLength, IEnumerable<int> productIds, DateTime? until = null)
        {
            var settings = _settingService.LoadSetting<SalesForecastingPluginSettings>();

            var data = GetData(productIds.ToArray());
            if (until != null)
                data = data.Where(s => s.Created <= until);
            if (!data.Any())
                return null;
            var request = new ForecastRequest
            {
                Data = data,
                Period = periodLength
            };
            var requestContent = new StringContent(JsonConvert.SerializeObject(request, _jsonSerializerSettings));
            requestContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            var response = await _httpClient.PostAsync($"{BASE_URL}forecast", requestContent).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var forecast = JsonConvert.DeserializeObject<ForecastResponse>(responseContent);
            foreach (var pid in productIds.Cast<string>().Except(forecast.Predictions.Keys))
                forecast.Predictions[pid] = 0;
            return forecast;
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
    }

    public class ForecastResponse
    {
        public IDictionary<string, float> BestParams { get; set; }
        public float BestScore { get; set; }
        public IDictionary<string, int> Predictions { get; set; }
    }
}
