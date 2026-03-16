# Resiliency Pattern Examples

This project demonstrates three resiliency patterns in a .NET Aspire application backed by Azure Cosmos DB: **Circuit Breaker**, **Saga**, and **Bulkhead Isolation**.

## Architecture

| Service | Component | Role |
|---|---|---|
| **ResiliencyPatterns.Web** | Blazor web frontend | Simple product catalog and order form |
| **ResiliencyPatterns.OrderService** | HTTP API | Reserves inventory, creates orders, calls the payment service |
| **ResiliencyPatterns.ProductsService** | Background worker + HTTP API | Serves product reads, listens to the Cosmos DB change feed and releases inventory on failed payments |
| **FlakeyPaymentService** | HTTP API | Simulates a 3rd-party payment service that randomly returns 500 errors (~66% failure rate) |

## Patterns

### Circuit Breaker

The [circuit breaker pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-circuit-breaker-pattern) prevents cascading failures by temporarily blocking calls to a failing downstream service.

**Implementation:** A Polly circuit breaker is registered on the named `HttpClient` that `OrderService` uses to call `FlakeyPaymentService`. After enough 500 responses accumulate within the sampling window, the circuit opens and subsequent calls fail immediately with `BrokenCircuitException` instead of waiting for another timeout. The `/order` endpoint catches this exception, marks the order as failed, and emits a compensation event.

- Configuration: `FailureRatio = 0.25`, `MinimumThroughput = 3`, `SamplingDuration = 30s`
- State transitions are logged to the console (`CB STATE: Open / Half open / Closed`)

### Saga (Choreography)

The [saga pattern](https://learn.microsoft.com/en-us/azure/architecture/reference-architectures/saga/saga) manages data consistency across services by using compensating transactions instead of distributed transactions.

**Implementation:** When an order is placed, `OrderService` reserves inventory (decrements the `inventory` field on product documents) *before* calling the payment service. If payment fails, the service writes a `PaymentFailedEvent` to the Cosmos DB `events` container and marks the order as `Failed`. Events carry the full product list with quantities and categories so the consumer needs no additional lookups. `ProductsService` runs a `ChangeFeedProcessor` that watches the `events` container. When it reads a `PaymentFailed` event, it increments inventory back on each affected product. This is called a compensating transaction, and it restores the system to a consistent state.

- Events container: `ProductsDB/events` (partition key: `/customerId`)
- Leases container: `ProductsDB/leases` (partition key: `/id`)

### Bulkhead Isolation

The [bulkhead pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead) isolates components so that a failure in one doesn't cascade to others.

**Implementation:** The web frontend uses two separate typed `HttpClient` registrations: `OrderServiceClient` for order submissions and `ProductsClient` for product inventory reads. Each client has its own independent connection pool and resilience pipeline. The circuit breaker is only attached to `OrderServiceClient`, so when the payment service triggers enough failures and the circuit opens, product catalog reads continue working normally through `ProductsClient`.

## Prerequisites

- .NET 9 SDK
- Azure Cosmos DB account

## Setup

### Cosmos DB containers

Create the following databases and containers in your Azure Cosmos DB account:

| Database | Container | Partition key |
|---|---|---|
| `OrdersDB` | `orders` | `/customerId` |
| `ProductsDB` | `products` | `/category` |
| `ProductsDB` | `events` | `/customerId` |
| `ProductsDB` | `leases` | `/id` |

### Configuration

Update the Cosmos DB endpoint in the following files:
- `ResiliencyPatterns.OrderService/appsettings.Development.json`
- `ResiliencyPatterns.ProductsService/appsettings.Development.json`

Update the three sample product IDs and categories in `ResiliencyPatterns.Web/Components/Pages/Home.razor` to match documents in your `products` container.

### Run

```cmd
cd ResiliencyPatterns.AppHost
dotnet run
```

### Demo walkthrough

1. Open the web frontend — the product grid loads with live inventory from Cosmos DB
2. Submit an order — inventory decrements immediately (reservation)
3. If payment succeeds → order confirmed, inventory stays decremented
4. If payment fails → order marked failed, `PaymentFailedEvent` emitted to `events` container
5. Click **Refresh Inventory** — after a few seconds the change feed processor compensates and inventory is restored
6. Submit several orders rapidly to trip the circuit breaker → observe the CB OPEN message
7. Note that product reads continue working even while the payment circuit is open (bulkhead isolation)

### Global secondary index

[Global secondary indexes](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/global-secondary-indexes) are a best practice for improving cross partition queries. GSIs are containers with a copy of data from a source container and have their own partition key, throughput, and indexing policy. Using a GSI can help reduce latency and RU cost, which contribute to overall application availability.

Create a global secondary index for the **products** container. Ensure the global secondary index feature is [enabled](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-configure-global-secondary-indexes?tabs=azure-portal%2Cdotnet#enable-global-secondary-indexes) on your account.
- GSI name: productsByBrand
- Source container: products
- Partition key: /brand

Before running this sample, update the values in **QueryProducts/appsettings.json**. Load data into your products container and see it automatically be synced in the GSI.

```cmd
cd QueryProducts
dotnet run
```
