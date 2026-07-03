var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder
    .AddGraphQL("Ordering")
    .ModifyServerOptions(x => x.Batching = HotChocolate.AspNetCore.AllowedBatching.All)
    .AddTypes().AddGlobalObjectIdentification(p => p.MarkNodeFieldAsLookup = true)
    .AddQueryType(d => d.Name("Query"));

var app = builder.Build();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);

public class QueryShareableExtension : ObjectTypeExtension
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        descriptor.Name("Query").Shareable();
    }
}