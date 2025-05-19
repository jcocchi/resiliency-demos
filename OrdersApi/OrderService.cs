using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace OrdersApi
{
    public class OrderService
    {
        private readonly Container _orders;
        
        public OrderService(IOptions<CosmosOptions> cosmosOptions)
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }
            };
            CosmosClient client = new CosmosClient(cosmosOptions.Value.Endpoint, new DefaultAzureCredential(), clientOptions);
            Database db = client.GetDatabase(cosmosOptions.Value.Database);
            _orders = db.GetContainer(cosmosOptions.Value.Container);
        }

        public async Task CreateOrder(Order order)
        {
            await _orders.UpsertItemAsync(order, new PartitionKey(order.Id));
        }
    }
}
