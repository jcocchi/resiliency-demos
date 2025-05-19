namespace OrdersApi
{
    public class Order
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public List<Item> Items { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public double TotalAmount { get; set; }
        public PaymentInfo Payment { get; set; }
        public string PromotionCode { get; set; }
        public Address ShippingAddress { get; set; }

    }

    public class Item
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
    }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }

    public class PaymentInfo
    {
        public string Method { get; set; }
        public string TransactionId { get; set; }
        public bool Paid { get; set; }
    }
}
