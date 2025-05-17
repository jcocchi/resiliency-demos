using Polly;
using Polly.CircuitBreaker;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddResilienceHandler(
        "CustomPipeline",
        static builder =>
        {
            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                // Customize and configure the circuit breaker logic.
                SamplingDuration = TimeSpan.FromSeconds(10),
                FailureRatio = 0.25,
                MinimumThroughput = 3,
                OnHalfOpened = args =>
                {
                    Console.WriteLine("CB STATE: Half open. Testing if circuit can be closed.");
                    return default;
                },
                OnClosed = args =>
                {
                    Console.WriteLine("CB STATE: Closed. Requests can go through.");
                    return default;
                },
                OnOpened = args =>
                {
                    Console.WriteLine("CB STATE: Open. Requests are temporarily blocked.");
                    return default;
                }
            });
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/order", async (IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("paymentServiceSimulator");
    string requestEndpoint = $"https://localhost:7275/flakeyPaymentService";

    try
    {
        HttpResponseMessage response =
            await httpClient.GetAsync(requestEndpoint);

        if (response.IsSuccessStatusCode)
        {
            var todo = await response.Content.ReadFromJsonAsync<dynamic>();
            return Results.Ok(todo);
        }
        return Results.InternalServerError("Something went wrong with payment processing.");
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"(CB CLOSED) Request failed without tripping circuit: {ex.Message}");
        return Results.InternalServerError("(CB CLOSED) Unable to process payment. Please try again.");
    }
    catch (BrokenCircuitException ex)
    {
        Console.WriteLine($"(CB OPEN) Request failed due to opened circuit: {ex.Message}");
        return Results.InternalServerError("(CB OPEN) Unable to process payment. Please try again later.");
    }
});

app.MapGet("/flakeyPaymentService", () =>
{
    Random random = new Random();
    if(random.Next(100) < 33)
    {
        return Results.InternalServerError("Error processing payment.");
    }

    return Results.Ok("Successfully processed payment!");
});

app.Run();
