using LiteDB;

namespace wangpucaiben.Models;

public sealed class ProductSalesRanking
{
    public ObjectId ProductId { get; set; } = ObjectId.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = "件";

    public decimal Quantity { get; set; }

    public decimal ProfitAmount { get; set; }
}
