using quick_start.Gateway.Utilities;
using HotChocolate.Execution;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCaptureRequestError() // <------ Add a DelegatingHandler to capture subgraph errors with non-null data
    .AddHttpClient("fusion");

var gatewayBuilder = builder
    .AddGraphQLGateway()
    .AddFileSystemConfiguration("./gateway.far")
    .UseRestoreRequestError(); // <------ Add a request middleware to restore captured errors into the final response

var requestExecutor = await gatewayBuilder.BuildRequestExecutorAsync();

var app = builder.Build();

//app.UseMiddleware<GraphQLListInputCoercionMiddleware>(requestExecutor);
app.MapGraphQL();

await app.RunAsync();

