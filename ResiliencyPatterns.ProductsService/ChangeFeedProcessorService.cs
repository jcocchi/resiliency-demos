using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ResiliencyPatterns.ProductsService
{
    public class ChangeFeedProcessorService : BackgroundService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly IOptions<CosmosOptions> _cosmosOptions;
        private readonly ILogger<ChangeFeedProcessorService> _logger;

        // In-memory idempotency guard — prevents double-processing on at-least-once delivery.
        // For a production system, replace with a durable processedEvents Cosmos container.
        private readonly HashSet<string> _processedEventIds = new();

        public ChangeFeedProcessorService(
            CosmosClient cosmosClient,
            IOptions<CosmosOptions> cosmosOptions,
            ILogger<ChangeFeedProcessorService> logger)
        {
            _cosmosClient = cosmosClient;
            _cosmosOptions = cosmosOptions;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _cosmosClient.GetDatabase(_cosmosOptions.Value.Database);
            var eventsContainer = db.GetContainer(_cosmosOptions.Value.EventsContainer);
            var leasesContainer = db.GetContainer(_cosmosOptions.Value.LeasesContainer);
            var productsContainer = db.GetContainer(_cosmosOptions.Value.ProductsContainer);

            var processor = eventsContainer
                .GetChangeFeedProcessorBuilder<PaymentFailedEvent>(
                    "inventoryCompensation",
                    async (IReadOnlyCollection<PaymentFailedEvent> changes, CancellationToken ct) =>
                    {
                        foreach (var failedEvent in changes)
                        {
                            if (failedEvent.EventType != "PaymentFailed")
                            {
                                continue;
                            }

                            if (!_processedEventIds.Add(failedEvent.Id))
                            {
                                _logger.LogWarning("Duplicate event {EventId} skipped", failedEvent.Id);
                                continue;
                            }

                            _logger.LogInformation(
                                "Processing PaymentFailed event {EventId} for order {OrderId}",
                                failedEvent.Id, failedEvent.OrderId);

                            foreach (var product in failedEvent.Products)
                            {
                                var patchOps = new[] { PatchOperation.Increment("/inventory", product.Quantity) };
                                await productsContainer.PatchItemAsync<dynamic>(
                                    product.ProductId,
                                    new PartitionKey(product.Category),
                                    patchOps,
                                    cancellationToken: ct);
                            }

                            _logger.LogInformation(
                                "Inventory released for order {OrderId}", failedEvent.OrderId);
                        }
                    })
                .WithInstanceName("products-service-1")
                .WithLeaseContainer(leasesContainer)
                .Build();

            await processor.StartAsync();

            await Task.Delay(Timeout.Infinite, stoppingToken);

            await processor.StopAsync();
        }
    }
}
