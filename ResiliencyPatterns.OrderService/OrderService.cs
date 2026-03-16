#nullable enable
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ResiliencyPatterns.OrderService
{
    public class OrderService
    {
        private readonly Container _orders;
        private readonly Container _products;
        private readonly Container _events;

        public OrderService(CosmosClient client, IOptions<CosmosOptions> cosmosOptions)
        {
            Database ordersDb = client.GetDatabase(cosmosOptions.Value.Database);
            _orders = ordersDb.GetContainer(cosmosOptions.Value.Container);

            Database productsDb = client.GetDatabase(cosmosOptions.Value.ProductsDatabase);
            _products = productsDb.GetContainer(cosmosOptions.Value.ProductsContainer);
            _events = productsDb.GetContainer(cosmosOptions.Value.EventsContainer);
        }

        public async Task<Order> CreateOrder(Order order)
        {
            order.Status = "PendingPayment";
            return await _orders.UpsertItemAsync(order, new PartitionKey(order.CustomerId));
        }

        public async Task ReserveInventory(Order order)
        {
            foreach (var product in order.Products)
            {
                var patchOps = new[] { PatchOperation.Increment("/inventory", -product.Quantity) };
                await _products.PatchItemAsync<dynamic>(product.ProductId, new PartitionKey(product.Category), patchOps);
            }
        }

        public async Task UpdateOrderStatus(Order order, string status)
        {
            var patchOps = new[] { PatchOperation.Replace("/status", status) };
            await _orders.PatchItemAsync<Order>(order.Id, new PartitionKey(order.CustomerId), patchOps);
        }

        public async Task EmitPaymentFailedEvent(Order order)
        {
            var failedEvent = new PaymentFailedEvent
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Products = order.Products
                    .Select(p => new EventProductItem { ProductId = p.ProductId, Category = p.Category, Quantity = p.Quantity })
                    .ToList()
            };
            await _events.CreateItemAsync(failedEvent, new PartitionKey(failedEvent.CustomerId));
        }
    }
}
