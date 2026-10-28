using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;

/// <summary>
/// Sinh ticket_code WMS theo Max (gồm soft-delete) để tránh đụng PK phiếu đã xóa.
/// </summary>
public static class TicketCodeAllocator
{
    public static async Task<string> NextAsync(
        WarehouseDbContext db,
        TransactionType type,
        CancellationToken ct = default)
    {
        var sample = WarehouseTransaction.GenerateTicketCode(type, 1);
        var dash = sample.IndexOf('-');
        var prefix = dash >= 0 ? sample[..(dash + 1)] : sample;

        var codes = await db.WarehouseTransactions
            .IgnoreQueryFilters()
            .Where(t => t.TransactionType == type)
            .Select(t => t.TicketCode)
            .ToListAsync(ct);

        var sequence = SequentialCodeHelper.NextSequence(codes, prefix);
        return WarehouseTransaction.GenerateTicketCode(type, sequence);
    }

    /// <summary>
    /// Khóa advisory theo loại phiếu trong transaction hiện tại (PostgreSQL).
    /// Bỏ qua trên provider không phải PostgreSQL (InMemory/SQLite tests).
    /// </summary>
    public static async Task AcquireTypeLockAsync(
        WarehouseDbContext db,
        TransactionType type,
        CancellationToken ct = default)
    {
        if (!db.Database.IsNpgsql())
            return;

        var lockKey = $"wms_ticket_{(int)type}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            ct);
    }

    public static async Task AcquireNamedLockAsync(
        DbContext db,
        string lockKey,
        CancellationToken ct = default)
    {
        if (!db.Database.IsNpgsql())
            return;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            ct);
    }
}
