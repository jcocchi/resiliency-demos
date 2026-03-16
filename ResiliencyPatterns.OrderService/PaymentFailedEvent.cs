namespace ResiliencyPatterns.OrderService
{
    public class PaymentFailedEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = "PaymentFailed";
        public string OrderId { get; set; }
        public string CustomerId { get; set; }
        public List<EventProductItem> Products { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class EventProductItem
    {
        public string ProductId { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
    }
}
