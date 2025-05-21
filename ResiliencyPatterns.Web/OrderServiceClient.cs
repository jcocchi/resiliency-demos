using Polly.CircuitBreaker;
using ResiliencyPatterns.OrderService;
using System.Net;

namespace ResiliencyPatterns.Web
{
    public class OrderServiceClient(HttpClient httpClient)
    {
        public async Task<HttpResponseMessage> PostOrder(Order order)
        {
            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsJsonAsync("/order", order);
                return response;
            }
            catch (BrokenCircuitException ex)
            {
                return new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("(CB OPEN) Unable to process payment. Please try again later.")
                };
            }
        }
    }
}
