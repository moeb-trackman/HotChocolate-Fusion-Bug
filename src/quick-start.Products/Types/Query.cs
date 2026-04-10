using HotChocolate.Fusion.SourceSchema.Types;

namespace quick_start.Products.Types;

[QueryType]
public static class Query
{
    private static readonly Product[] _products = [
            new Product
            {
                Id = 1,
                Name = "Product 1",
            },
            new Product
            {
                Id = 2,
                Name = "Product 2",
            }
        ];

    public static Product[] GetProducts()
    {
        return _products;
    }

    [Query]
    [HotChocolate.Fusion.SourceSchema.Types.Lookup]
    [HotChocolate.Fusion.SourceSchema.Types.Internal]
    [NodeResolver]
    public static Task<Product> GetProductById(int id, ProductByIdDataLoader productById)
    {
        //Use data loader to avoid n+1 problem
        return productById.LoadAsync(id);
    }
}

public class ProductByIdDataLoader(
    IBatchScheduler batchScheduler,
    DataLoaderOptions options) : BatchDataLoader<int, Product>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<int, Product>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        return keys.Select(id => new Product
        {
            Id = id,
            Name = $"Product {id}",
        }).ToDictionary(p => p.Id, p => p);
    }
}
