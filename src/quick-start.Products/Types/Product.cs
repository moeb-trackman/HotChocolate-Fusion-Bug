namespace quick_start.Products.Types;

public record Product
{
    [Shareable]
    public int Id { get; init; }
    public string Name { get; init; }
}