using System.Text.Json;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ResiliencyPatterns.OrderService;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults_NoResilience();

ILogger? circuitBreakerLogger = null;

builder.RegisterConfiguration();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Configure Azure Cosmos DB Aspire integration
var cosmosEndpoint = builder.Configuration.GetSection(nameof(CosmosOptions)).GetValue<string>("Endpoint");
if (cosmosEndpoint is null)
{
    throw new ArgumentException($"{nameof(IOptions<CosmosOptions>)} was not resolved through dependency injection.");
}
builder.AddAzureCosmosClient(
    "cosmos",
    settings =>
    {
        settings.AccountEndpoint = new Uri(cosmosEndpoint);
        settings.Credential = new DefaultAzureCredential();
        settings.DisableTracing = false;
    },
    clientOptions => {
        clientOptions.ApplicationRegion = Regions.WestUS2;
        clientOptions.UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        clientOptions.CosmosClientTelemetryOptions.CosmosThresholdOptions = new CosmosThresholdOptions()
        {
            PointOperationLatencyThreshold = TimeSpan.FromMilliseconds(1),
            NonPointOperationLatencyThreshold = TimeSpan.FromMilliseconds(10)
        };
    });

builder.Services.RegisterServices();

// Register named HttpClient for the flakey payment service with circuit breaker
builder.Services.AddHttpClient("flakey3rdPartyPaymentClient", client =>
    {
        client.BaseAddress = new("https+http://flakeypaymentservice");
    })
    .AddResilienceHandler("PaymentCircuitBreaker", resilienceBuilder =>
    {
        resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.25,
            MinimumThroughput = 3,
            OnHalfOpened = args =>
            {
                circuitBreakerLogger?.LogWarning("CB STATE: Half open. Testing if circuit can be closed.");
                return default;
            },
            OnClosed = args =>
            {
                circuitBreakerLogger?.LogInformation("CB STATE: Closed. Requests can go through.");
                return default;
            },
            OnOpened = args =>
            {
                circuitBreakerLogger?.LogError("CB STATE: Open. Requests are temporarily blocked.");
                return default;
            }
        });
    });

var app = builder.Build();
circuitBreakerLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PaymentCircuitBreaker");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.MapPost("/order", async (Order order, OrderService orderService, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("OrderEndpoint");

    // Store order information in Azure Cosmos DB
    var orderResponse = await orderService.CreateOrder(order);

    // Reserve inventory before attempting payment
    await orderService.ReserveInventory(order);

    // Process order payment
    var httpClient = httpClientFactory.CreateClient("flakey3rdPartyPaymentClient");
    string requestEndpoint = $"/createFlakey3rdPartyPayment";

    try
    {
        HttpResponseMessage response = await httpClient.PostAsync(requestEndpoint, null);
        if (response.IsSuccessStatusCode)
        {
            await orderService.UpdateOrderStatus(orderResponse, "Confirmed");
            var result = await response.Content.ReadFromJsonAsync<string>();
            logger.LogInformation("(CB CLOSED) Request succeeded.");
            return Results.Ok(result);
        }
        await orderService.UpdateOrderStatus(orderResponse, "Failed");
        await orderService.EmitPaymentFailedEvent(orderResponse);
        logger.LogWarning("(CB CLOSED) Request failed without tripping circuit");
        return Results.InternalServerError("(CB CLOSED) Something went wrong with payment processing. Request failed without tripping circuit.");
    }
    catch (BrokenCircuitException ex)
    {
        await orderService.UpdateOrderStatus(orderResponse, "Failed");
        await orderService.EmitPaymentFailedEvent(orderResponse);
        logger.LogWarning("(CB OPEN) Request failed due to opened circuit");
        return Results.InternalServerError("(CB OPEN) Unable to process payment. Please try again later.");
    }
});

app.MapDefaultEndpoints();

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