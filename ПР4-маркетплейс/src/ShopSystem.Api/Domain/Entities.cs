namespace ShopSystem.Api.Domain;

public sealed class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Customer";
    public string? Address { get; set; }
    public List<CartItem> CartItems { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Favorite> Favorites { get; set; } = [];
    public List<ProductReview> Reviews { get; set; } = [];
}

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string ImagePath { get; set; } = "";
    public int Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public List<CartItem> CartItems { get; set; } = [];
    public List<Favorite> Favorites { get; set; } = [];
    public List<ProductReview> Reviews { get; set; } = [];
}

public sealed class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime AddedAtUtc { get; set; }
}

public sealed class ProductReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PickupPoint
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}

public enum OrderStatus { Placed, Processing, Shipped, ReadyForPickup, Completed, Cancelled }
public enum FulfillmentType { AddressDelivery, PickupPoint }

public sealed class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public FulfillmentType Fulfillment { get; set; }
    public string Destination { get; set; } = "";
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public string ProductNameAtPurchase { get; set; } = "";
    public decimal UnitPriceAtPurchase { get; set; }
    public int Quantity { get; set; }
}
