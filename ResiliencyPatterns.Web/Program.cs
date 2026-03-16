using Microsoft.Extensions.Http.Resilience;
using Polly;
using ResiliencyPatterns.Web;
using ResiliencyPatterns.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults_CircuitBreaker();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<OrderServiceClient>(client =>
    {
        client.BaseAddress = new("https+http://orderservice");
    })
    .AddResilienceHandler("OrderServiceCircuitBreaker", static builder =>
    {
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
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

builder.Services.AddHttpClient<ProductsClient>(client =>
    {
        client.BaseAddress = new("https+http://productsservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
