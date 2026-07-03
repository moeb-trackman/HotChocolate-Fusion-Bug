namespace quick_start.Products.Types;

public class ProductType : ObjectType<Product>
{
    protected override void Configure(IObjectTypeDescriptor<Product> descriptor)
    {
        descriptor
            .Name("Product")
            .BindFieldsExplicitly();

        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode(async (ctx, identifier) => Repository.GetProduct(identifier));

        descriptor.Field(x => x.Name);
        descriptor.Field(x => x.Description);
    }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public static class Repository
{
    private static readonly List<Product> List =
    [
        new Product
        {
            Id = 1001,
            Name = "Product 1",
            Description = "Description for Product 1"
        },
        new Product
        {
            Id = 1002,
            Name = "Product 2",
            Description = "Description for Product 2"
        },
        new Product
        {
            Id = 1003,
            Name = "Product 3",
            Description = "Description for Product 3"
        },
        new Product
        {
            Id = 1004,
            Name = "Product 4",
            Description = "Description for Product 4"
        }
    ];

    public static Product GetProduct(int id)
    {
        return List.FirstOrDefault(product => product.Id == id);
    }
}