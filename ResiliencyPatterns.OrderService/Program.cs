using System.Text.Json;
using Polly.CircuitBreaker;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ResiliencyPatterns.OrderService;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults_CircuitBreaker();

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
    });

builder.Services.RegisterServices();

var app = builder.Build();

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

app.MapPost("/order", async (Order order, OrderService orderService, IHttpClientFactory httpClientFactory) =>
{
    // Store order information in Azure Cosmos DB
    await orderService.CreateOrder(order);

    // Process order payment
    var httpClient = httpClientFactory.CreateClient("flakey3rdPartyPaymentClient");
    httpClient.BaseAddress = new("https+http://flakeypaymentservice");
    string requestEndpoint = $"/createFlakey3rdPartyPayment";

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
        return Results.InternalServerError("Something went wrong with payment processing. Request failed without tripping circuit.");
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