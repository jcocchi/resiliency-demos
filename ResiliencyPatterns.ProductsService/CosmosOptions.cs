namespace ResiliencyPatterns.ProductsService
{
    public class CosmosOptions
    {
        public required string Endpoint { get; init; }

        public required string Database { get; init; }

        public required string ProductsContainer { get; init; }

        public required string EventsContainer { get; init; }

        public required string LeasesContainer { get; init; }
    }
}
