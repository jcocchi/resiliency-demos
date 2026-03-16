var builder = DistributedApplication.CreateBuilder(args);

var paymentService = builder.AddProject<Projects.FlakeyPaymentService>("flakeypaymentservice");

var orderService = builder.AddProject<Projects.ResiliencyPatterns_OrderService>("orderservice")
    .WithReference(paymentService)
    .WaitFor(paymentService);

var productsService = builder.AddProject<Projects.ResiliencyPatterns_ProductsService>("productsservice")
    .WaitFor(orderService);

builder.AddProject<Projects.ResiliencyPatterns_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(orderService)
    .WithReference(productsService)
    .WaitFor(orderService)
    .WaitFor(productsService);

builder.Build().Run();
