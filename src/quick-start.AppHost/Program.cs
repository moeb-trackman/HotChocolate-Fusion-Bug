var builder = DistributedApplication.CreateBuilder(args);

var ordering = builder.AddProject<Projects.quick_start_Ordering>("ordering", "quick-start.Ordering").WithUrl("http://localhost:5002/graphql");
var products = builder.AddProject<Projects.quick_start_Products>("products", "quick-start.Products").WithUrl("http://localhost:5003/graphql");

builder
    .AddProject<Projects.quick_start_Gateway>("gateway")
    .WithReference(ordering)
    .WithReference(products);

await builder.Build().RunAsync();