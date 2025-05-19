using Polly;
using Polly.CircuitBreaker;
using Microsoft.Extensions.Http.Resilience;
using OrdersApi;

var builder = WebApplication.CreateBuilder(args);

builder.RegisterConfiguration();
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
                    Console.Error.Write("CB STATE: Open. Requests are temporarily blocked.");
                    return default;
                }
            });
        });
});

builder.Services.RegisterServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/order", async (Order order, OrderService orderService, IHttpClientFactory httpClientFactory) =>
{
    // Store order information in Azure Cosmos DB
    await orderService.CreateOrder(order);
    
    // Process order payment
    var httpClient = httpClientFactory.CreateClient("flakey3rdPartyPaymentClient");
    string requestEndpoint = $"https://localhost:7275/createFlakey3rdPartyPayment";

    try
    {
        HttpResponseMessage response = await httpClient.GetAsync(requestEndpoint);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<string>();
            Console.WriteLine($"(CB CLOSED) Request succeeded");
            return Results.Ok(result);
        }
        Console.Error.WriteLine($"(CB CLOSED) Request failed without tripping circuit");
        return Results.InternalServerError("Something went wrong with payment processing.");
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"(CB CLOSED) Request failed without tripping circuit");
        return Results.InternalServerError("(CB CLOSED) Unable to process payment. Please try again.");
    }
    catch (BrokenCircuitException ex)
    {
        Console.Error.WriteLine($"(CB OPEN) Request failed due to opened circuit");
        return Results.InternalServerError("(CB OPEN) Unable to process payment. Please try again later.");
    }
});

app.MapGet("/createFlakey3rdPartyPayment", () =>
{
    Random random = new Random();
    if(random.Next(100) < 50)
    {
        return Results.InternalServerError("Error processing payment.");
    }

    return Results.Ok("Successfully processed payment!");
});

app.Run();

static class ProgramExtensions
{
    public static void RegisterConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CosmosOptions>()
            .Bind(builder.Configuration.GetSection(nameof(CosmosOptions)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<OrderService, OrderService>();
    }
}