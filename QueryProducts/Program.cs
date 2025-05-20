using QueryProducts;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

CosmosClientOptions clientOptions = new CosmosClientOptions()
{
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }
};
CosmosClient client = new CosmosClient(config["Endpoint"], new DefaultAzureCredential(), clientOptions);
Container source = client.GetDatabase(config["Database"]).GetContainer(config["SourceContainerName"]);
Container gsi = client.GetDatabase(config["Database"]).GetContainer(config["GSIName"]);

Console.WriteLine($"Hello, welcome to the Azure Cosmos DB GSI demo!");
Console.WriteLine($"----------------------------------------------------------- \n\n");


Console.WriteLine("Finding products by brand.");
Console.WriteLine($"-----------------------------------------------------------");

var findProductsByBrand = "SELECT TOP 1000 * FROM c WHERE c.brand = \"Zivana\"";
var statsSource = await RunQuery(source, findProductsByBrand);
var statsGSI = await RunQuery(gsi, findProductsByBrand);

PrintComparisonOutput(statsSource, statsGSI, findProductsByBrand);


Console.WriteLine("Finding products by brand and name.");
Console.WriteLine($"-----------------------------------------------------------");

var findProductsByBrandAndName = "SELECT * FROM c WHERE c.brand = \"Zivana\" and CONTAINS(c.name, \"Gloves\")";
var statsSource2 = await RunQuery(source, findProductsByBrandAndName);
var statsGSI2 = await RunQuery(gsi, findProductsByBrandAndName);

PrintComparisonOutput(statsSource2, statsGSI2, findProductsByBrandAndName);


static async Task<QueryStats> RunQuery(Container container, string queryText)
{
    var query = new QueryDefinition(queryText);

    Console.WriteLine($"Running against container {container.Id}.");
    Console.WriteLine($"\t* Query: {queryText}\n");

    var requestCharge = 0.0;
    var executionTime = new TimeSpan();
    var results = new List<dynamic>();

    var resultSetIterator = container.GetItemQueryIterator<dynamic>(query, null, new QueryRequestOptions() { PopulateIndexMetrics = true });
    while (resultSetIterator.HasMoreResults)
    {
        var response = await resultSetIterator.ReadNextAsync();
        results.AddRange(response.Resource);
        requestCharge += response.RequestCharge;
        executionTime += response.Diagnostics.GetClientElapsedTime();

        Console.WriteLine($"Trip num items: {response.Count}, Trip request charge: {response.RequestCharge}, Trip execution time: {response.Diagnostics.GetClientElapsedTime()}");
    }

    Console.WriteLine($"Final Request charge: {requestCharge}, Final execution time: {executionTime}, Total items: {results.Count}\n\n");

    var stats = new QueryStats()
    {
        RUCharge = requestCharge,
        ExecutionTime = executionTime
    };

    return stats;
}

static void PrintComparisonOutput(QueryStats sourceStats, QueryStats gsiStats, string queryText)
{
    Console.WriteLine($"\nShowing final results for query \"{queryText}\"");
    Console.WriteLine($"-----------------------------------------------------------");

    Console.WriteLine("|Setup            |RU Charge |Execution Time  |");
    Console.WriteLine("|-----------------|----------|----------------|");
    Console.WriteLine("|Source container |{0, -10}|{1, -16}|", Math.Round(sourceStats.RUCharge, 2), sourceStats.ExecutionTime);
    Console.WriteLine("|GSI container    |{0, -10}|{1, -16}|", Math.Round(gsiStats.RUCharge, 2), gsiStats.ExecutionTime);

    Console.WriteLine("Press enter to continue...");
    Console.ReadLine();
    Console.WriteLine();
}