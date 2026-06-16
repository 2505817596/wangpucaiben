using LiteDB;
using wangpucaiben.Data;
using wangpucaiben.Models;

namespace wangpucaiben.Services;

public sealed class ProductService
{
    private readonly ILiteCollection<Product> _products;

    public ProductService(LiteDbContext context)
    {
        _products = context.Collection<Product>();
        _products.EnsureIndex("Name");
        _products.EnsureIndex("Barcode");
    }

    public List<Product> GetAll()
    {
        return _products.FindAll()
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    public Product? GetById(ObjectId id)
    {
        return _products.FindById(id);
    }

    public Product? GetByBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var normalizedBarcode = barcode.Trim();
        return _products.FindAll()
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Barcode) &&
                string.Equals(x.Barcode.Trim(), normalizedBarcode, StringComparison.OrdinalIgnoreCase));
    }

    public Product Upsert(Product product)
    {
        product.UpdatedAt = DateTimeOffset.Now;
        if (product.Id == ObjectId.Empty)
        {
            product.Id = ObjectId.NewObjectId();
        }

        _products.Upsert(product);
        return product;
    }

    public bool Delete(ObjectId id)
    {
        return _products.Delete(id);
    }
}
