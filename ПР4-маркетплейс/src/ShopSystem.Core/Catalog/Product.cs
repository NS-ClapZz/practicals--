namespace ShopSystem.Core.Catalog;

public sealed class Product
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }
}

