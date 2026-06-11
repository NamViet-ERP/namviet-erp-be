using MediatR;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.Transactions;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Handlers;

public class ApproveTransactionHandler(
    WarehouseDbContext db,
    ICurrentUser currentUser)
    : IRequestHandler<ApproveTransactionCommand, string>
{
    public async Task<string> Handle(ApproveTransactionCommand command, CancellationToken ct)
    {
        var transaction = await db.WarehouseTransactions
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.TicketCode == command.TicketCode, ct)
            ?? throw new KeyNotFoundException($"Phiếu '{command.TicketCode}' không tồn tại.");

        if (transaction.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException(
                $"Phiếu đã ở trạng thái '{transaction.ApprovalStatus}', không thể duyệt lại.");

        transaction.ApprovalStatus = command.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        transaction.ApprovedById = currentUser.Id;
        transaction.ApprovedByName = currentUser.Name;
        transaction.ApprovedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (!command.Approve && !string.IsNullOrEmpty(command.Reason))
            transaction.Notes = (transaction.Notes ?? string.Empty) + $"\n[Từ chối] {command.Reason}";

        if (command.Approve && transaction.ProjectCode is not null)
            await UpdateInventoryAsync(transaction, ct);

        await db.SaveChangesAsync(ct);
        return transaction.ApprovalStatus.ToString();
    }

    private async Task UpdateInventoryAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        foreach (var item in transaction.Items)
        {
            var inv = await db.ProjectInventories
                .FirstOrDefaultAsync(i =>
                    i.ProjectCode == transaction.ProjectCode &&
                    i.MaterialCode == item.MaterialCode, ct);

            if (inv is null)
            {
                inv = new ProjectInventory
                {
                    ProjectCode = transaction.ProjectCode!,
                    MaterialCode = item.MaterialCode
                };
                db.ProjectInventories.Add(inv);
            }

            switch (transaction.TransactionType)
            {
                case TransactionType.Import:
                case TransactionType.LoanImport:
                    inv.ActualImportQty += item.Quantity;
                    break;
                case TransactionType.Export:
                case TransactionType.LoanExport:
                case TransactionType.Transfer:
                    inv.ActualExportQty += item.Quantity;
                    break;
            }
            inv.UpdatedAt = DateTime.UtcNow;
        }
    }
}
