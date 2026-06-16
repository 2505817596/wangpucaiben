using LiteDB;
using wangpucaiben.Data;
using wangpucaiben.Models;

namespace wangpucaiben.Services;

public sealed class TransactionService
{
    private readonly ILiteCollection<CashTransaction> _transactions;

    public TransactionService(LiteDbContext context)
    {
        _transactions = context.Collection<CashTransaction>();
        _transactions.EnsureIndex("OccurredAt");
        _transactions.EnsureIndex("TransactionType");
        _transactions.EnsureIndex("SourceOrderNo");
    }

    public List<CashTransaction> GetRecent(int limit = 50)
    {
        return _transactions.FindAll()
            .ToList()
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToList();
    }

    public decimal GetIncomeTotal(DateTimeOffset start, DateTimeOffset end)
    {
        return _transactions.FindAll()
            .ToList()
            .Where(x =>
                x.TransactionType == CashTransactionType.Income &&
                x.OccurredAt >= start &&
                x.OccurredAt < end)
            .Sum(x => x.Amount);
    }

    public List<DailySalesPoint> GetDailyIncomeSeries(DateTimeOffset start, DateTimeOffset end)
    {
        var offset = start.Offset;
        var dailyTotals = _transactions.FindAll()
            .ToList()
            .Where(x =>
                x.TransactionType == CashTransactionType.Income &&
                x.OccurredAt >= start &&
                x.OccurredAt < end)
            .GroupBy(x => DateOnly.FromDateTime(x.OccurredAt.ToOffset(offset).Date))
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));

        var points = new List<DailySalesPoint>();
        for (var cursor = DateOnly.FromDateTime(start.Date); cursor < DateOnly.FromDateTime(end.Date); cursor = cursor.AddDays(1))
        {
            points.Add(new DailySalesPoint
            {
                Date = cursor,
                Amount = dailyTotals.GetValueOrDefault(cursor, 0m)
            });
        }

        return points;
    }

    public List<CashTransaction> GetBySourceOrderNo(string sourceOrderNo)
    {
        if (string.IsNullOrWhiteSpace(sourceOrderNo))
        {
            return [];
        }

        return _transactions.FindAll()
            .ToList()
            .Where(x =>
                string.Equals(x.SourceOrderNo, sourceOrderNo, StringComparison.Ordinal) ||
                (x.Note?.Contains(sourceOrderNo, StringComparison.Ordinal) ?? false))
            .ToList();
    }

    public CashTransaction Upsert(CashTransaction transaction)
    {
        if (transaction.Id == ObjectId.Empty)
        {
            transaction.Id = ObjectId.NewObjectId();
        }

        transaction.CreatedAt = transaction.CreatedAt == default
            ? DateTimeOffset.Now
            : transaction.CreatedAt;

        _transactions.Upsert(transaction);
        return transaction;
    }

    public void DeleteBySourceOrderNo(string sourceOrderNo)
    {
        foreach (var transaction in GetBySourceOrderNo(sourceOrderNo))
        {
            _transactions.Delete(transaction.Id);
        }
    }
}
