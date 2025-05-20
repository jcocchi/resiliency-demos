using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ResiliencyPatterns.OrderService
{
    public class OrderService
    {
        private readonly Container _orders;

        public OrderService(CosmosClient client, IOptions<CosmosOptions> cosmosOptions)
        {
            Database db = client.GetDatabase(cosmosOptions.Value.Database);
            _orders = db.GetContainer(cosmosOptions.Value.Container);
        }

        public async Task CreateOrder(Order order)
        {
            await _orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));
        }
    }
}
