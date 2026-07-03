var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder
    .AddGraphQL("Products")
    .ModifyServerOptions(x => x.Batching = HotChocolate.AspNetCore.AllowedBatching.All)
    .AddTypes().AddGlobalObjectIdentification(p => p.MarkNodeFieldAsLookup = true)
    .AddQueryType(p => p.Name("Query"));

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