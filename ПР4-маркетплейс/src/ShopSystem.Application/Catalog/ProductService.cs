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
            throw new ArgumentException("Product ID must be positive.", nameof(product));
        }

        if (string.IsNullOrWhiteSpace(product.Name))
        {
            throw new ArgumentException("Product name is required.", nameof(product));
        }

        if (product.Price < 0)
        {
            throw new ArgumentException("Product price cannot be negative.", nameof(product));
        }

        if (repository.Products.Any(existingProduct => existingProduct.Id == product.Id))
        {
            throw new InvalidOperationException($"Product with ID {product.Id} already exists.");
        }

        repository.AddProduct(product);
    }
}

