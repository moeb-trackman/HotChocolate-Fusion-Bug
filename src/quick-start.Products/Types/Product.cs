namespace quick_start.Products.Types;

public record Product
{
    [Shareable]
    public int Id { get; init; }
    public string Name { get; init; }
    public Comment[] Comments { get; init; }
}

public class Comment
{
    public string Content { get; init; }
}