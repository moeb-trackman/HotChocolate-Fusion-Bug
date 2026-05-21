var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder
    .AddGraphQL("Products")
    .ModifyServerOptions(x => x.Batching = HotChocolate.AspNetCore.AllowedBatching.All)
    .AddTypes().AddGlobalObjectIdentification(p => p.MarkNodeFieldAsLookup = true);

var app = builder.Build();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);