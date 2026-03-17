using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ResiliencyPatterns.ProductsService;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<CosmosOptions>()
    .Bind(builder.Configuration.GetSection(nameof(CosmosOptions)));

var cosmosEndpoint = builder.Configuration.GetSection(nameof(CosmosOptions)).GetValue<string>("Endpoint");
if (cosmosEndpoint is null)
{
    throw new ArgumentException("CosmosOptions.Endpoint was not configured.");
}

builder.AddAzureCosmosClient(
    "cosmos",
    settings =>
    {
        settings.AccountEndpoint = new Uri(cosmosEndpoint);
        settings.Credential = new DefaultAzureCredential();
        settings.DisableTracing = false;
    },
    clientOptions =>
    {
        clientOptions.UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    });

builder.Services.AddHostedService<ChangeFeedProcessorService>();

var app = builder.Build();

app.MapGet("/products", async (string ids, string categories, CosmosClient cosmosClient, IOptions<CosmosOptions> options) =>
{
    var db = cosmosClient.GetDatabase(options.Value.Database);
    var container = db.GetContainer(options.Value.ProductsContainer);
    var productIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries);
    var productCategories = categories.Split(',', StringSplitOptions.RemoveEmptyEntries);
    var tasks = productIds.Zip(productCategories, async (id, cat) =>
    {
        try
        {
            var response = await container.ReadItemAsync<ProductRecord>(id, new PartitionKey(cat));
            return response.Resource;
        }
        catch (CosmosException)
        {
            return null;
        }
    });
    var results = await Task.WhenAll(tasks);
    return Results.Ok(results.OfType<ProductRecord>().ToList());
});

app.MapDefaultEndpoints();

app.Run();
