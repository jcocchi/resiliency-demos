using Polly.CircuitBreaker;
using ResiliencyPatterns.OrderService;

namespace ResiliencyPatterns.Web
{
    public class ProductsClient(HttpClient httpClient)
    {
        public async Task<List<ProductInventory>> GetProductInventories((string Id, string Category)[] products)
        {
            var ids = string.Join(",", products.Select(p => p.Id));
            var categories = string.Join(",", products.Select(p => p.Category));
            try
            {
                var result = await httpClient.GetFromJsonAsync<List<ProductInventory>>($"/products?ids={ids}&categories={categories}");
                return result ?? [];
            }
            catch (BrokenCircuitException)
            {
                return [];
            }
        }
    }
}
