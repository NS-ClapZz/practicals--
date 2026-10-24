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
    Name = "Механическая клавиатура",
    Price = 12990m
});

productService.AddProduct(new Product
{
    Id = 2,
    Name = "Беспроводная мышь",
    Price = 4990m
});

Console.WriteLine("=== Каталог ShopSystem ===");
Console.WriteLine("Список товаров:");

foreach (var product in productService.GetProducts())
{
    Console.WriteLine($"{product.Id}: {product.Name} — {product.Price} ₽");
}

var order = orderService.CreateOrder(
    customerName: "Иван Петров",
    productIds: [1, 2]);

Console.WriteLine();
Console.WriteLine($"Заказ №{order.Id} (покупатель: {order.CustomerName})");
Console.WriteLine($"Итого к оплате: {order.TotalAmount} ₽");
