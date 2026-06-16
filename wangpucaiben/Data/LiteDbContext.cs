using LiteDB;
using wangpucaiben.Models;

namespace wangpucaiben.Data;

public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;

    public LiteDbContext(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);

        var databasePath = configuration["LiteDb:DatabasePath"];
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine(dataFolder, "ledger.db");
        }
        else if (!Path.IsPathRooted(databasePath))
        {
            databasePath = Path.Combine(environment.ContentRootPath, databasePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _database = new LiteDatabase($"Filename={databasePath};Connection=shared");
        MigrateNumericFields();
    }

    public ILiteCollection<T> Collection<T>(string? name = null)
    {
        return _database.GetCollection<T>(name ?? typeof(T).Name);
    }

    private void MigrateNumericFields()
    {
        ConvertFieldToDecimal("Product", "StockQuantity");
        ConvertFieldToDecimal("Product", "PurchasePrice");
        ConvertFieldToDecimal("Product", "SalePrice");
        ConvertFieldToDecimal("InventoryMovement", "Quantity");
        ConvertFieldToDecimal("InventoryMovement", "UnitCost");
        ConvertFieldToDecimal("PurchaseOrder", "TotalAmount");
        ConvertFieldToDecimal("PurchaseOrder", "Items[*].Quantity");
        ConvertFieldToDecimal("PurchaseOrder", "Items[*].UnitPrice");
        ConvertFieldToDecimal("SalesOrder", "TotalAmount");
        ConvertFieldToDecimal("SalesOrder", "TotalCost");
        ConvertFieldToDecimal("SalesOrder", "ProfitAmount");
        ConvertFieldToDecimal("SalesOrder", "Items[*].Quantity");
        ConvertFieldToDecimal("SalesOrder", "Items[*].UnitPrice");
        ConvertFieldToDecimal("SalesOrder", "Items[*].LineTotalAmount");
        ConvertFieldToDecimal("SalesOrder", "Items[*].CostPrice");
        ConvertFieldToDecimal("CashTransaction", "Amount");
        MigrateSalesOrderProfitFields();
        MigrateInventoryMovementProductNames();
    }

    private void ConvertFieldToDecimal(string collectionName, string fieldPath)
    {
        var collection = _database.GetCollection(collectionName);
        foreach (var document in collection.FindAll().ToList())
        {
            if (TryConvertPath(document, fieldPath))
            {
                collection.Update(document);
            }
        }
    }

    private static bool TryConvertPath(BsonDocument document, string fieldPath)
    {
        var segments = fieldPath.Split('.');
        return TryConvertSegments(document, segments, 0);
    }

    private static bool TryConvertSegments(BsonDocument document, string[] segments, int index)
    {
        var segment = segments[index];
        if (segment.EndsWith("[*]", StringComparison.Ordinal))
        {
            var arrayName = segment[..^3];
            if (!document.TryGetValue(arrayName, out var arrayValue) || !arrayValue.IsArray)
            {
                return false;
            }

            var changed = false;
            foreach (var item in arrayValue.AsArray)
            {
                if (item.IsDocument)
                {
                    changed |= TryConvertSegments(item.AsDocument, segments, index + 1);
                }
            }

            return changed;
        }

        if (index == segments.Length - 1)
        {
            if (!document.TryGetValue(segment, out var value))
            {
                return false;
            }

            if (value.Type == BsonType.Int32)
            {
                document[segment] = new BsonValue(Convert.ToDecimal(value.AsInt32));
                return true;
            }

            if (value.Type == BsonType.Int64)
            {
                document[segment] = new BsonValue(Convert.ToDecimal(value.AsInt64));
                return true;
            }

            return false;
        }

        if (!document.TryGetValue(segment, out var nextValue) || !nextValue.IsDocument)
        {
            return false;
        }

        return TryConvertSegments(nextValue.AsDocument, segments, index + 1);
    }

    private void MigrateSalesOrderProfitFields()
    {
        var productCollection = _database.GetCollection("Product");
        var salesOrderCollection = _database.GetCollection("SalesOrder");
        var productCostMap = productCollection.FindAll()
            .Where(x => x.TryGetValue("_id", out var id) && id.IsObjectId)
            .ToDictionary(
                x => x["_id"].AsObjectId,
                x => GetDecimalValue(x, "PurchasePrice"));

        foreach (var order in salesOrderCollection.FindAll().ToList())
        {
            if (!order.TryGetValue("Items", out var itemsValue) || !itemsValue.IsArray)
            {
                continue;
            }

            var changed = false;
            decimal totalAmount = 0m;
            decimal totalCost = 0m;

            foreach (var itemValue in itemsValue.AsArray)
            {
                if (!itemValue.IsDocument)
                {
                    continue;
                }

                var item = itemValue.AsDocument;
                var quantity = GetDecimalValue(item, "Quantity");
                var unitPrice = GetDecimalValue(item, "UnitPrice");
                var lineTotalAmount = GetDecimalValue(item, "LineTotalAmount");
                var costPrice = GetDecimalValue(item, "CostPrice");
                var pricingMode = GetIntValue(item, "PricingMode");

                if (!item.TryGetValue("PricingMode", out _))
                {
                    item["PricingMode"] = new BsonValue((int)SalesOrderPricingMode.UnitPrice);
                    pricingMode = (int)SalesOrderPricingMode.UnitPrice;
                    changed = true;
                }

                if (lineTotalAmount <= 0m)
                {
                    lineTotalAmount = quantity * unitPrice;
                    item["LineTotalAmount"] = new BsonValue(lineTotalAmount);
                    changed = true;
                }

                if (costPrice <= 0m &&
                    item.TryGetValue("ProductId", out var productIdValue) &&
                    productIdValue.IsObjectId &&
                    productCostMap.TryGetValue(productIdValue.AsObjectId, out var productCost))
                {
                    costPrice = productCost;
                    item["CostPrice"] = new BsonValue(costPrice);
                    changed = true;
                }

                var itemAmount = pricingMode == (int)SalesOrderPricingMode.BulkTotal
                    ? lineTotalAmount
                    : quantity * unitPrice;

                totalAmount += itemAmount;
                totalCost += quantity * costPrice;
            }

            var profitAmount = totalAmount - totalCost;
            changed |= SetDecimalValue(order, "TotalAmount", totalAmount);
            changed |= SetDecimalValue(order, "TotalCost", totalCost);
            changed |= SetDecimalValue(order, "ProfitAmount", profitAmount);

            if (changed)
            {
                salesOrderCollection.Update(order);
            }
        }
    }

    private void MigrateInventoryMovementProductNames()
    {
        var inventoryMovementCollection = _database.GetCollection("InventoryMovement");
        var productCollection = _database.GetCollection("Product");
        var purchaseOrderCollection = _database.GetCollection("PurchaseOrder");
        var salesOrderCollection = _database.GetCollection("SalesOrder");

        var productNameMap = productCollection.FindAll()
            .Where(x => x.TryGetValue("_id", out var id) && id.IsObjectId)
            .ToDictionary(
                x => x["_id"].AsObjectId,
                x => GetStringValue(x, "Name"));

        foreach (var movement in inventoryMovementCollection.FindAll().ToList())
        {
            if (!string.IsNullOrWhiteSpace(GetStringValue(movement, "ProductName")))
            {
                continue;
            }

            var productId = movement.TryGetValue("ProductId", out var productIdValue) && productIdValue.IsObjectId
                ? productIdValue.AsObjectId
                : ObjectId.Empty;

            var sourceOrderNo = GetStringValue(movement, "SourceOrderNo");
            var productName = productNameMap.GetValueOrDefault(productId, string.Empty);

            if (string.IsNullOrWhiteSpace(productName) && !string.IsNullOrWhiteSpace(sourceOrderNo))
            {
                productName = FindProductNameFromOrderItems(purchaseOrderCollection, sourceOrderNo, productId)
                    ?? FindProductNameFromOrderItems(salesOrderCollection, sourceOrderNo, productId)
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(productName))
            {
                continue;
            }

            movement["ProductName"] = new BsonValue(productName);
            inventoryMovementCollection.Update(movement);
        }
    }

    private static string? FindProductNameFromOrderItems(ILiteCollection<BsonDocument> orderCollection, string orderNo, ObjectId productId)
    {
        var order = orderCollection.FindAll()
            .FirstOrDefault(x => string.Equals(GetStringValue(x, "OrderNo"), orderNo, StringComparison.Ordinal));

        if (order is null || !order.TryGetValue("Items", out var itemsValue) || !itemsValue.IsArray)
        {
            return null;
        }

        foreach (var itemValue in itemsValue.AsArray)
        {
            if (!itemValue.IsDocument)
            {
                continue;
            }

            var item = itemValue.AsDocument;
            var itemProductId = item.TryGetValue("ProductId", out var itemProductIdValue) && itemProductIdValue.IsObjectId
                ? itemProductIdValue.AsObjectId
                : ObjectId.Empty;

            if (itemProductId != productId)
            {
                continue;
            }

            var productName = GetStringValue(item, "ProductName");
            if (!string.IsNullOrWhiteSpace(productName))
            {
                return productName;
            }
        }

        return null;
    }

    private static decimal GetDecimalValue(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value))
        {
            return 0m;
        }

        return value.Type switch
        {
            BsonType.Decimal => value.AsDecimal,
            BsonType.Int32 => Convert.ToDecimal(value.AsInt32),
            BsonType.Int64 => Convert.ToDecimal(value.AsInt64),
            BsonType.Double => Convert.ToDecimal(value.AsDouble),
            BsonType.String when decimal.TryParse(value.AsString, out var parsed) => parsed,
            _ => 0m
        };
    }

    private static int GetIntValue(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value))
        {
            return 0;
        }

        return value.Type switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => Convert.ToInt32(value.AsInt64),
            BsonType.Decimal => Convert.ToInt32(value.AsDecimal),
            BsonType.Double => Convert.ToInt32(value.AsDouble),
            BsonType.String when int.TryParse(value.AsString, out var parsed) => parsed,
            _ => 0
        };
    }

    private static string GetStringValue(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value))
        {
            return string.Empty;
        }

        return value.Type == BsonType.String ? value.AsString : value.RawValue?.ToString() ?? string.Empty;
    }

    private static bool SetDecimalValue(BsonDocument document, string fieldName, decimal value)
    {
        var currentValue = GetDecimalValue(document, fieldName);
        if (document.TryGetValue(fieldName, out var rawValue) &&
            rawValue.Type == BsonType.Decimal &&
            currentValue == value)
        {
            return false;
        }

        document[fieldName] = new BsonValue(value);
        return true;
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
