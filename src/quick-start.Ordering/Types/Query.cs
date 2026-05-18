using HotChocolate.Resolvers;

namespace quick_start.Ordering.Types;

[QueryType]
[Shareable]
public static class Query
{
    private static readonly IOrder[] _orders =
        [
            new Order1
            {
                Id = 1,
                Name = "Order1",
                Product = new Product(1)
            },
            new Order2
            {
                Id = 2,
                Name = "Order2",
                Product = new Product(2)
            }
        ];

    [NodeResolver]
    public static Order1? GetOrder1ById(int id) => _orders.FirstOrDefault(x => x.Id == id) as Order1;

    [NodeResolver]
    public static Order2? GetOrder2ById(int id) => _orders.FirstOrDefault(x => x.Id == id) as Order2;

    public static IOrder[] GetOrders(IResolverContext context)
    {
        context.ReportError("This is an error message from the resolver.");
        return _orders;
    }
}