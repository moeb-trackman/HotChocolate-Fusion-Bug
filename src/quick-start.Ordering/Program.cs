var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddGraphQL("Ordering").AddTypes().AddGlobalObjectIdentification(p => p.MarkNodeFieldAsLookup = true);

var app = builder.Build();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);
