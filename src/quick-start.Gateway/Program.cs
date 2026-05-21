using quick_start.Gateway.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCaptureRequestError() // <------ Add a DelegatingHandler to capture subgraph errors with non-null data
    .AddHttpClient("fusion");

builder
    .AddGraphQLGateway()
    .AddFileSystemConfiguration("./gateway.far")
    .UseRestoreRequestError(); // <------ Add a request middleware to restore captured errors into the final response

var app = builder.Build();

app.MapGraphQL();

await app.RunAsync();

