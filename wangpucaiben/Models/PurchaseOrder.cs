using LiteDB;

namespace wangpucaiben.Models;

public sealed class PurchaseOrderItem
{
    public ObjectId ProductId { get; set; } = ObjectId.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = "件";

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount => Quantity * UnitPrice;
}

public sealed class PurchaseOrder
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public string OrderNo { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public List<PurchaseOrderItem> Items { get; set; } = [];

    public decimal TotalAmount { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
