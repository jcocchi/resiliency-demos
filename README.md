# Resiliency Pattern Examples

## Circuit Breaker

The [circuit breaker pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern) is implemented using a custom resilience handler with Polly in our .NET Aspire project. The circuit breaker helps temporarily block calls to failing services and prevent them from being overwhelmed.

Create an Azure Cosmos DB account with a container for orders.
- Database name: OrdersDB
- Container name: orders
- Partition key: /customerId

Before running the sample, add your Azure Cosmos DB account endpoint in **ResiliencyPatterns.OrderService/appsettings.json**.

```cmd
cd ResiliencyPatterns.AppHost
dotnet run
```

## Global secondary index

[Global secondary indexes](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/global-secondary-indexes) in Azure Cosmos DB help improve query performance for cross partition queries. GSIs are containers with a copy of data from a source container and are automatically kept in sync as data in the source container changes. Because GSIs are independent containers, they have their own partition key, throughput, indexing policy and any other container properties.

Create a container for products in your Azure Cosmos DB account.
- Database name: ProductsDB
- Container name: products
- Partition key: /category

Create a global secondary index for the **products** container. Ensure the global secondary index feature is [enabled](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-configure-global-secondary-indexes?tabs=azure-portal%2Cdotnet#enable-global-secondary-indexes) on your account.
- GSI name: productsByBrand
- Source container: products
- Partition key: /brand

Before running this sample, update the values in **QueryProducts/appsettings.json**. Load data into your products container and see it automatically be synced in the GSI.

```cmd
cd QueryProducts
dotnet run
```
