using LiteDB;
using wangpucaiben.Data;
using wangpucaiben.Models;

namespace wangpucaiben.Services;

public sealed class OrderService
{
    private readonly ILiteCollection<PurchaseOrder> _purchaseOrders;
    private readonly ILiteCollection<SalesOrder> _salesOrders;
    private readonly ILiteCollection<Product> _products;
    private readonly InventoryService _inventoryService;
    private readonly TransactionService _transactionService;

    public OrderService(
        LiteDbContext context,
        InventoryService inventoryService,
        TransactionService transactionService)
    {
        _purchaseOrders = context.Collection<PurchaseOrder>();
        _salesOrders = context.Collection<SalesOrder>();
        _products = context.Collection<Product>();
        _inventoryService = inventoryService;
        _transactionService = transactionService;

        _purchaseOrders.EnsureIndex("OrderNo");
        _purchaseOrders.EnsureIndex("OccurredAt");
        _salesOrders.EnsureIndex("OrderNo");
        _salesOrders.EnsureIndex("OccurredAt");
    }

    public List<PurchaseOrder> GetRecentPurchaseOrders(int limit = 30)
    {
        return _purchaseOrders.FindAll()
            .ToList()
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToList();
    }

    public List<SalesOrder> GetRecentSalesOrders(int limit = 30)
    {
        return _salesOrders.FindAll()
            .ToList()
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToList();
    }

    public List<SalesOrder> GetSalesOrdersByDate(DateTime date)
    {
        var start = new DateTimeOffset(date.Date, DateTimeOffset.Now.Offset);
        var end = start.AddDays(1);

        return GetSalesOrdersByDateRange(start, end);
    }

    public List<SalesOrder> GetSalesOrdersByDateRange(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        return _salesOrders.FindAll()
            .ToList()
            .Where(x => x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
            .OrderByDescending(x => x.OccurredAt)
            .ToList();
    }

    public List<ProductSalesRanking> GetTopSellingProducts(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        int limit = 10)
    {
        return _salesOrders.FindAll()
            .ToList()
            .Where(x => x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
            .SelectMany(x => x.Items)
            .Where(x => x.Quantity > 0 && (!string.IsNullOrWhiteSpace(x.ProductName) || x.ProductId != ObjectId.Empty))
            .GroupBy(x => x.ProductId != ObjectId.Empty ? x.ProductId.ToString() : x.ProductName.Trim())
            .Select(group =>
            {
                var first = group.First();
                var productName = group
                    .Select(x => x.ProductName?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "未命名商品";
                var unit = group
                    .Select(x => x.Unit?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "件";

                return new ProductSalesRanking
                {
                    ProductId = first.ProductId,
                    ProductName = productName,
                    Unit = unit,
                    Quantity = group.Sum(x => x.Quantity),
                    ProfitAmount = group.Sum(x => x.ProfitAmount)
                };
            })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductName)
            .Take(limit)
            .ToList();
    }

    public List<ProductSalesRanking> GetTopProfitProducts(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        int limit = 10)
    {
        return _salesOrders.FindAll()
            .ToList()
            .Where(x => x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
            .SelectMany(x => x.Items)
            .Where(x => x.Quantity > 0 && (!string.IsNullOrWhiteSpace(x.ProductName) || x.ProductId != ObjectId.Empty))
            .GroupBy(x => x.ProductId != ObjectId.Empty ? x.ProductId.ToString() : x.ProductName.Trim())
            .Select(group =>
            {
                var first = group.First();
                var productName = group
                    .Select(x => x.ProductName?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "未命名商品";
                var unit = group
                    .Select(x => x.Unit?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? "件";

                return new ProductSalesRanking
                {
                    ProductId = first.ProductId,
                    ProductName = productName,
                    Unit = unit,
                    Quantity = group.Sum(x => x.Quantity),
                    ProfitAmount = group.Sum(x => x.ProfitAmount)
                };
            })
            .OrderByDescending(x => x.ProfitAmount)
            .ThenByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductName)
            .Take(limit)
            .ToList();
    }

    public decimal GetProfitTotal(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        return _salesOrders.FindAll()
            .ToList()
            .Where(x => x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
            .Sum(x => x.ProfitAmount);
    }

    public List<DailySalesPoint> GetDailyProfitSeries(DateTimeOffset startInclusive, DateTimeOffset endExclusive)
    {
        var offset = startInclusive.Offset;
        var dailyTotals = _salesOrders.FindAll()
            .ToList()
            .Where(x => x.OccurredAt >= startInclusive && x.OccurredAt < endExclusive)
            .GroupBy(x => DateOnly.FromDateTime(x.OccurredAt.ToOffset(offset).Date))
            .ToDictionary(x => x.Key, x => x.Sum(item => item.ProfitAmount));

        var points = new List<DailySalesPoint>();
        for (var cursor = DateOnly.FromDateTime(startInclusive.Date); cursor < DateOnly.FromDateTime(endExclusive.Date); cursor = cursor.AddDays(1))
        {
            points.Add(new DailySalesPoint
            {
                Date = cursor,
                Amount = dailyTotals.GetValueOrDefault(cursor, 0m)
            });
        }

        return points;
    }

    public PurchaseOrder? GetPurchaseOrderById(ObjectId id)
    {
        return _purchaseOrders.FindById(id);
    }

    public SalesOrder? GetSalesOrderById(ObjectId id)
    {
        return _salesOrders.FindById(id);
    }

    public PurchaseOrder CreatePurchaseOrder(PurchaseOrder order)
    {
        order.Id = order.Id == ObjectId.Empty ? ObjectId.NewObjectId() : order.Id;
        order.OrderNo = string.IsNullOrWhiteSpace(order.OrderNo) ? BuildOrderNo("CG") : order.OrderNo;
        order.TotalAmount = order.Items.Sum(x => x.Amount);
        order.CreatedAt = DateTimeOffset.Now;

        ApplyPurchaseSideEffects(order);
        _purchaseOrders.Insert(order);
        return order;
    }

    public PurchaseOrder UpdatePurchaseOrder(PurchaseOrder order)
    {
        var existing = _purchaseOrders.FindById(order.Id)
            ?? throw new InvalidOperationException("采购单不存在");

        order.OrderNo = existing.OrderNo;
        order.CreatedAt = existing.CreatedAt;
        order.TotalAmount = order.Items.Sum(x => x.Amount);

        RemovePurchaseSideEffects(existing.OrderNo);
        ApplyPurchaseSideEffects(order);
        _purchaseOrders.Update(order);
        return order;
    }

    public void DeletePurchaseOrder(ObjectId id)
    {
        var existing = _purchaseOrders.FindById(id);
        if (existing is null)
        {
            return;
        }

        RemovePurchaseSideEffects(existing.OrderNo);
        _purchaseOrders.Delete(id);
    }

    public SalesOrder CreateSalesOrder(SalesOrder order)
    {
        order.Id = order.Id == ObjectId.Empty ? ObjectId.NewObjectId() : order.Id;
        order.OrderNo = string.IsNullOrWhiteSpace(order.OrderNo) ? BuildOrderNo("XS") : order.OrderNo;
        order.CustomerName = NormalizeCustomerName(order.CustomerName);
        order.Note = NormalizeSalesOrderNote(order);
        ApplySalesCostSnapshot(order);
        order.TotalAmount = order.Items.Sum(x => x.Amount);
        order.TotalCost = order.Items.Sum(x => x.CostAmount);
        order.ProfitAmount = order.TotalAmount - order.TotalCost;
        order.CreatedAt = DateTimeOffset.Now;

        ApplySalesSideEffects(order);
        _salesOrders.Insert(order);
        return order;
    }

    public SalesOrder UpdateSalesOrder(SalesOrder order)
    {
        var existing = _salesOrders.FindById(order.Id)
            ?? throw new InvalidOperationException("销售单不存在");

        RemoveSalesSideEffects(existing.OrderNo);

        order.OrderNo = existing.OrderNo;
        order.CreatedAt = existing.CreatedAt;
        order.CustomerName = NormalizeCustomerName(order.CustomerName);
        order.Note = NormalizeSalesOrderNote(order);
        ApplySalesCostSnapshot(order);
        order.TotalAmount = order.Items.Sum(x => x.Amount);
        order.TotalCost = order.Items.Sum(x => x.CostAmount);
        order.ProfitAmount = order.TotalAmount - order.TotalCost;

        ApplySalesSideEffects(order);
        _salesOrders.Update(order);
        return order;
    }

    public void DeleteSalesOrder(ObjectId id)
    {
        var existing = _salesOrders.FindById(id);
        if (existing is null)
        {
            return;
        }

        RemoveSalesSideEffects(existing.OrderNo);
        _salesOrders.Delete(id);
    }

    private void ApplyPurchaseSideEffects(PurchaseOrder order)
    {
        foreach (var item in order.Items.Where(x => x.ProductId != ObjectId.Empty && x.Quantity > 0))
        {
            _inventoryService.AddMovement(new InventoryMovement
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                MovementType = InventoryMovementType.PurchaseIn,
                Quantity = item.Quantity,
                UnitCost = item.UnitPrice,
                SourceOrderNo = order.OrderNo,
                Note = $"采购单 {order.OrderNo}"
            });
        }

        _transactionService.Upsert(new CashTransaction
        {
            TransactionType = CashTransactionType.Expense,
            Amount = order.TotalAmount,
            SourceOrderNo = order.OrderNo,
            Category = "采购支出",
            Note = $"采购单 {order.OrderNo} {order.SupplierName}".Trim(),
            OccurredAt = order.OccurredAt
        });
    }

    private void RemovePurchaseSideEffects(string orderNo)
    {
        _inventoryService.DeleteBySourceOrderNo(orderNo);
        _transactionService.DeleteBySourceOrderNo(orderNo);
    }

    private void ApplySalesSideEffects(SalesOrder order)
    {
        foreach (var item in order.Items.Where(x => x.ProductId != ObjectId.Empty && x.Quantity > 0))
        {
            _inventoryService.AddMovement(new InventoryMovement
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                MovementType = InventoryMovementType.SaleOut,
                Quantity = item.Quantity,
                UnitCost = item.CostPrice,
                SourceOrderNo = order.OrderNo,
                Note = $"销售单 {order.OrderNo}"
            });
        }

        _transactionService.Upsert(new CashTransaction
        {
            TransactionType = CashTransactionType.Income,
            Amount = order.TotalAmount,
            SourceOrderNo = order.OrderNo,
            Category = "销售收入",
            Note = $"销售单 {order.OrderNo} {order.CustomerName}".Trim(),
            OccurredAt = order.OccurredAt
        });
    }

    private void RemoveSalesSideEffects(string orderNo)
    {
        _inventoryService.DeleteBySourceOrderNo(orderNo);
        _transactionService.DeleteBySourceOrderNo(orderNo);
    }

    private void ApplySalesCostSnapshot(SalesOrder order)
    {
        foreach (var item in order.Items)
        {
            if (item.PricingMode == SalesOrderPricingMode.BulkTotal)
            {
                if (item.LineTotalAmount <= 0m)
                {
                    item.LineTotalAmount = item.Quantity * item.UnitPrice;
                }
            }
            else
            {
                item.PricingMode = SalesOrderPricingMode.UnitPrice;
                item.LineTotalAmount = item.Quantity * item.UnitPrice;
            }
        }

        foreach (var item in order.Items.Where(x => x.ProductId != ObjectId.Empty && x.Quantity > 0))
        {
            if (item.CostPrice > 0)
            {
                continue;
            }

            var product = _products.FindById(item.ProductId);
            item.CostPrice = product?.PurchasePrice ?? 0m;
        }
    }

    private static string NormalizeCustomerName(string? customerName)
    {
        return string.IsNullOrWhiteSpace(customerName) ? "散客" : customerName.Trim();
    }

    private static string NormalizeSalesOrderNote(SalesOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.Note))
        {
            return order.Note.Trim();
        }

        return string.Join("+", order.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductName))
            .Select(x => x.ProductName.Trim())
            .Distinct());
    }

    private static string BuildOrderNo(string prefix)
    {
        return $"{prefix}{DateTime.Now:yyyyMMddHHmmss}";
    }
}
