namespace ResiliencyPatterns.OrderService
{
    public class CosmosOptions
    {
        public required string Endpoint { get; init; }

        public required string Database { get; init; }

        public required string Container { get; init; }
    }
}
