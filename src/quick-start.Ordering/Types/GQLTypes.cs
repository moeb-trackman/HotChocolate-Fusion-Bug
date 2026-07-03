
namespace quick_start.Ordering;

public class MeQueries : ObjectTypeExtension
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        descriptor
            .Name("Query");

        descriptor
            .Field("orders")
            .Type<ListType<NonNullType<OrderBaseInterfaceType>>>()
            .Resolve(ctx =>
            {
                List<OrderBase> orders = [
                    new OrderA
                    {
                        Name = "Order A",
                        Items =
                        [
                            new OrderItem
                            {
                                ProductId = 1001,
                                Quantity = 5,
                            },
                            new OrderItem
                            {
                                ProductId = 1002,
                                Quantity = 5,
                            }
                        ]
                    },
                    new OrderB
                    {
                        Name = "Order B",
                        Items =
                        [
                            new OrderItem
                            {
                                Quantity = 5,
                                ProductId = 1003,
                            },
                            new OrderItem
                            {
                                Quantity = 5,
                                ProductId = 1004,
                            }
                        ]
                    }
                ];
                return orders;
            });
    }
}

public class OrderBaseInterfaceType : InterfaceType<OrderBase>
{
    protected override void Configure(IInterfaceTypeDescriptor<OrderBase> descriptor)
    {
        descriptor
           .Name("OrderBase")
           .BindFieldsExplicitly();

        descriptor
            .Field(x => x.Name)
            .Type<StringType>();
    }
}

public class MultiOrderBaseInterfaceType : InterfaceType<MultiOrderBase>
{
    protected override void Configure(IInterfaceTypeDescriptor<MultiOrderBase> descriptor)
    {
        descriptor
           .Name("MultiOrderBase")
           .Implements<OrderBaseInterfaceType>()
           .BindFieldsExplicitly();

        descriptor
            .Field(c => c.Items)
            .Type<ListType<NonNullType<OrderItemType>>>();
    }
}

public class OrderItemType : ObjectType<OrderItem>
{
    protected override void Configure(IObjectTypeDescriptor<OrderItem> descriptor)
    {
        descriptor
            .Name("OrderItem")
            .BindFieldsExplicitly();

        descriptor
            .Field(x => new Product { Id = x.ProductId })
            .Name("product")
            .Type<ProductType>();
    }
}

public class OrderAType : ObjectType<OrderA>
{
    protected override void Configure(IObjectTypeDescriptor<OrderA> descriptor)
    {
        descriptor
            .Name("OrderA")
            .BindFieldsExplicitly()
            .Implements<MultiOrderBaseInterfaceType>();

        descriptor
            .Field(c => c.Name);

        descriptor
            .Field(c => c.Items)
            .Type<ListType<NonNullType<OrderItemType>>>();
    }
}
public class OrderBType : ObjectType<OrderB>
{
    protected override void Configure(IObjectTypeDescriptor<OrderB> descriptor)
    {
        descriptor
            .Name("OrderB")
            .BindFieldsExplicitly()
            .Implements<MultiOrderBaseInterfaceType>();

        descriptor
            .Field(c => c.Name);

        descriptor
            .Field(c => c.Items)
            .Type<ListType<NonNullType<OrderItemType>>>();
    }
}

public class ProductType : ObjectType<Product>
{
    protected override void Configure(IObjectTypeDescriptor<Product> descriptor)
    {
        descriptor
            .Name("Product")
            .BindFieldsExplicitly();

        descriptor.ImplementsNode()
            .IdField(x => x.Id)
            .ResolveNode((ctx, id) => null!);
    }
}

public abstract class OrderBase
{
    public string Name { get; set; }
}

public abstract class MultiOrderBase : OrderBase
{
    public OrderItem[] Items { get; set; }
}

public class OrderA : MultiOrderBase
{
}
public class OrderB : MultiOrderBase
{
}

public class OrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class Product
{
    public int Id { get; set; }
}