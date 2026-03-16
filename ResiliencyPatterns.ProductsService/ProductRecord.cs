namespace ResiliencyPatterns.ProductsService
{
    internal class ProductRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Price { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Inventory { get; set; }
    }
}
