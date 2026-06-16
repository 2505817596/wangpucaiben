using LiteDB;

namespace wangpucaiben.Models;

public sealed class Product
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string Unit { get; set; } = "件";

    public decimal PurchasePrice { get; set; }

    public decimal SalePrice { get; set; }

    public decimal StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
