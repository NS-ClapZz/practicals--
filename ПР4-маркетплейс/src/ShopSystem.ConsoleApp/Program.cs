using System.Text;
using ShopSystem.Application.Catalog;
using ShopSystem.Application.Orders;
using ShopSystem.Core.Catalog;
using ShopSystem.Infrastructure.Persistence;

Console.OutputEncoding = Encoding.UTF8;

var repository = new InMemoryShopRepository();
var productService = new ProductService(repository);
var orderService = new OrderService(repository);

productService.AddProduct(new Product
{
    Id = 1,
    Name = "Mechanical Keyboard",
    Price = 12990m
});

productService.AddProduct(new Product
{
    Id = 2,
    Name = "Wireless Mouse",
    Price = 4990m
});

Console.WriteLine("=== ShopSystem ===");
Console.WriteLine("Products:");

foreach (var product in productService.GetProducts())
{
    Console.WriteLine($"{product.Id}: {product.Name} — {product.Price} ₽");
}

var order = orderService.CreateOrder(
    customerName: "Ivan Petrov",
    productIds: [1, 2]);

Console.WriteLine();
Console.WriteLine($"Order #{order.Id} for {order.CustomerName}");
Console.WriteLine($"Total: {order.TotalAmount} ₽");
