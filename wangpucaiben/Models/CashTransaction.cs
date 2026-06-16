using LiteDB;

namespace wangpucaiben.Models;

public enum CashTransactionType
{
    Income = 1,
    Expense = 2
}

public sealed class CashTransaction
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public CashTransactionType TransactionType { get; set; }

    public decimal Amount { get; set; }

    public string? SourceOrderNo { get; set; } = string.Empty;

    public string? Category { get; set; } = string.Empty;

    public string? Note { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
