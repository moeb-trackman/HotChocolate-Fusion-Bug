var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddGraphQL("Products").AddTypes().AddGlobalObjectIdentification();

var app = builder.Build();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);