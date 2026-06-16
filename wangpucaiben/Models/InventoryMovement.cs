using LiteDB;

namespace wangpucaiben.Models;

public enum InventoryMovementType
{
    PurchaseIn = 1,
    SaleOut = 2,
    Adjustment = 3
}

public sealed class InventoryMovement
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public ObjectId ProductId { get; set; } = ObjectId.Empty;

    public string ProductName { get; set; } = string.Empty;

    public InventoryMovementType MovementType { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public string? SourceOrderNo { get; set; } = string.Empty;

    public string? Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
