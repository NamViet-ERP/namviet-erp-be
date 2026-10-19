using MediatR;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.Transactions;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Handlers;

public class ApproveTransactionHandler(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
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
            throw new DomainRuleException(
                "Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        transaction.ApprovalStatus = command.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        transaction.ApprovedById = currentUser.Id;
        transaction.ApprovedByName = currentUser.Name;
        transaction.ApprovedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (!command.Approve && !string.IsNullOrEmpty(command.Reason))
            transaction.Notes = (transaction.Notes ?? string.Empty) + $"\n[Từ chối] {command.Reason}";

        if (command.Approve)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            switch (transaction.TransactionType)
            {
                case TransactionType.Import:
                    await inventory.ApplyApprovedImportAsync(transaction, ct);
                    break;
                case TransactionType.Export:
                    await inventory.ApplyApprovedExportAsync(transaction, ct);
                    break;
                case TransactionType.Transfer:
                    if (!string.IsNullOrWhiteSpace(transaction.ProjectCode)
                        && !string.IsNullOrWhiteSpace(transaction.BeneficiaryProjectCode)
                        && !string.Equals(
                            transaction.ProjectCode,
                            transaction.BeneficiaryProjectCode,
                            StringComparison.OrdinalIgnoreCase))
                        await inventory.ApplyCrossProjectTransferAsync(transaction, ct);
                    else if (!string.IsNullOrWhiteSpace(transaction.ProjectCode))
                        await inventory.ApplyReturnToGeneralAsync(transaction, ct);
                    else
                        await inventory.ApplyTransferAsync(transaction, ct);
                    break;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else if (transaction.TransactionType == TransactionType.Import)
        {
            await inventory.CancelReservationsAsync(transaction.TicketCode, ct);
        }

        await db.SaveChangesAsync(ct);
        return transaction.ApprovalStatus.ToString();
    }
}
