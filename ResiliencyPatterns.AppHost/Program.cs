var builder = DistributedApplication.CreateBuilder(args);

var paymentService = builder.AddProject<Projects.FlakeyPaymentService>("flakeypaymentservice");

var orderService = builder.AddProject<Projects.ResiliencyPatterns_OrderService>("orderservice")
    .WithReference(paymentService)
    .WaitFor(paymentService);

builder.AddProject<Projects.ResiliencyPatterns_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(orderService)
    .WaitFor(orderService);

builder.Build().Run();
