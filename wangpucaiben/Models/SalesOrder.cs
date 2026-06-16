using LiteDB;

namespace wangpucaiben.Models;

public enum SalesOrderPricingMode
{
    UnitPrice = 0,
    BulkTotal = 1
}

public sealed class SalesOrderItem
{
    public ObjectId ProductId { get; set; } = ObjectId.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = "件";

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public SalesOrderPricingMode PricingMode { get; set; } = SalesOrderPricingMode.UnitPrice;

    public decimal LineTotalAmount { get; set; }

    public decimal CostPrice { get; set; }

    public decimal Amount => PricingMode == SalesOrderPricingMode.BulkTotal
        ? LineTotalAmount
        : Quantity * UnitPrice;

    public decimal CostAmount => Quantity * CostPrice;

    public decimal ProfitAmount => Amount - CostAmount;
}

public sealed class SalesOrder
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public string OrderNo { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public List<SalesOrderItem> Items { get; set; } = [];

    public decimal TotalAmount { get; set; }

    public decimal TotalCost { get; set; }

    public decimal ProfitAmount { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
