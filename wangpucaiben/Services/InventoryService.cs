using LiteDB;
using wangpucaiben.Data;
using wangpucaiben.Models;

namespace wangpucaiben.Services;

public sealed class InventoryService
{
    private readonly ILiteCollection<InventoryMovement> _movements;
    private readonly ILiteCollection<Product> _products;

    public InventoryService(LiteDbContext context)
    {
        _movements = context.Collection<InventoryMovement>();
        _products = context.Collection<Product>();
        _movements.EnsureIndex("ProductId");
        _movements.EnsureIndex("CreatedAt");
        _movements.EnsureIndex("SourceOrderNo");
    }

    public List<InventoryMovement> GetRecentMovements(int limit = 50)
    {
        return _movements.FindAll()
            .ToList()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public List<InventoryMovement> GetBySourceOrderNo(string sourceOrderNo)
    {
        if (string.IsNullOrWhiteSpace(sourceOrderNo))
        {
            return [];
        }

        return _movements.FindAll()
            .ToList()
            .Where(x =>
                string.Equals(x.SourceOrderNo, sourceOrderNo, StringComparison.Ordinal) ||
                (x.Note?.Contains(sourceOrderNo, StringComparison.Ordinal) ?? false))
            .ToList();
    }

    public InventoryMovement AddMovement(InventoryMovement movement)
    {
        if (movement.Id == ObjectId.Empty)
        {
            movement.Id = ObjectId.NewObjectId();
        }

        _movements.Insert(movement);
        ApplyDelta(movement.ProductId, movement.MovementType, movement.Quantity);
        return movement;
    }

    public void DeleteBySourceOrderNo(string sourceOrderNo)
    {
        var movements = GetBySourceOrderNo(sourceOrderNo);
        foreach (var movement in movements)
        {
            ApplyDelta(movement.ProductId, ReverseType(movement.MovementType), movement.Quantity);
            _movements.Delete(movement.Id);
        }
    }

    private void ApplyDelta(ObjectId productId, InventoryMovementType movementType, decimal quantity)
    {
        var product = _products.FindById(productId);
        if (product is null)
        {
            return;
        }

        var delta = movementType switch
        {
            InventoryMovementType.PurchaseIn => quantity,
            InventoryMovementType.SaleOut => -quantity,
            InventoryMovementType.Adjustment => quantity,
            _ => 0m
        };

        product.StockQuantity += delta;
        product.UpdatedAt = DateTimeOffset.Now;
        _products.Update(product);
    }

    private static InventoryMovementType ReverseType(InventoryMovementType movementType)
    {
        return movementType switch
        {
            InventoryMovementType.PurchaseIn => InventoryMovementType.SaleOut,
            InventoryMovementType.SaleOut => InventoryMovementType.PurchaseIn,
            _ => InventoryMovementType.Adjustment
        };
    }
}
