namespace quick_start.Ordering.Types;

public interface IOrder
{
    [ID]
    int Id { get; set; }
    string Name { get; set; }
    Product Product { get; set; }
}

[Node]
public class Order1 : IOrder
{
    [ID]
    public int Id { get; set; }

    public string Name { get; set; }
    public Product Product { get; set; }

}

[Node]
public class Order2 : IOrder
{
    [ID]
    public int Id { get; set; }

    public string Name { get; set; }
    public Product Product { get; set; }
}

public sealed record Product([property: ID] int Id);