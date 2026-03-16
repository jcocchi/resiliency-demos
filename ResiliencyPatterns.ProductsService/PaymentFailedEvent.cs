namespace ResiliencyPatterns.ProductsService
{
    public class PaymentFailedEvent
    {
        public string Id { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public List<EventProductItem> Products { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class EventProductItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
