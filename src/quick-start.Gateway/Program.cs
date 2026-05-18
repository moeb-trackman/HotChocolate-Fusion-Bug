var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("fusion");

builder
    .AddGraphQLGateway()
    .AddFileSystemConfiguration("./gateway.far")
    // do not enable query plan in production!
    .ModifyRequestOptions(x => x.CollectOperationPlanTelemetry = true);

var app = builder.Build();

app.MapGraphQL();

app.Run();