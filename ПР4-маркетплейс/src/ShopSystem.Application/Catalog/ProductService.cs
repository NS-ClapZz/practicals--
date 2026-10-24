using ShopSystem.Application.Abstractions;
using ShopSystem.Core.Catalog;

namespace ShopSystem.Application.Catalog;

public sealed class ProductService(IShopRepository repository)
{
    public IReadOnlyCollection<Product> GetProducts() => repository.Products;

    public void AddProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.Id <= 0)
        {
            throw new ArgumentException("Идентификатор товара должен быть положительным числом.", nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.Name))
        {
            throw new ArgumentException("Название товара обязательно для заполнения.", nameof(product));
        }

        if (product.Price < 0)
        {
            throw new ArgumentException("Цена товара не может быть отрицательной.", nameof(product));
        }

        if (repository.Products.Any(p => p.Id == product.Id))
        {
            throw new InvalidOperationException($"Товар с идентификатором {product.Id} уже существует.");
        }

        repository.AddProduct(product);
    }
}
