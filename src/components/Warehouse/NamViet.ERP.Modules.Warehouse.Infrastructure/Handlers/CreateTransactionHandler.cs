using MediatR;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Application.Transactions;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Handlers;

public class CreateTransactionHandler(
    WarehouseDbContext db,
    ICurrentUser currentUser)
    : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(CreateTransactionCommand command, CancellationToken ct)
    {
        var req = command.Request;

        return await SequentialCodeHelper.ExecuteWithUniqueRetryAsync(async () =>
        {
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await TicketCodeAllocator.AcquireTypeLockAsync(db, req.TransactionType, ct);
            var ticketCode = await TicketCodeAllocator.NextAsync(db, req.TransactionType, ct);

            var materialCodes = req.Items.Select(i => i.MaterialCode).Distinct().ToList();
            var materials = await db.Materials
                .Where(m => materialCodes.Contains(m.MaterialCode))
                .ToDictionaryAsync(m => m.MaterialCode, ct);

            foreach (var code in materialCodes)
                if (!materials.ContainsKey(code))
                    throw new DomainRuleException($"Vật tư '{code}' không tồn tại.");
            TransactionItemHelper.EnsureActiveMaterials(materialCodes, materials);

            var transaction = new WarehouseTransaction
            {
                Id = Guid.NewGuid(),
                TicketCode = ticketCode,
                TransactionType = req.TransactionType,
                ProjectCode = req.ProjectCode,
                SupplierId = req.SupplierId,
                InvoiceNumber = req.InvoiceNumber,
                DnttNumber = req.DnttNumber,
                Notes = req.Notes,
                CreatedById = currentUser.Id,
                CreatedByName = currentUser.Name,
                ApprovalStatus = ApprovalStatus.Pending,
                Items = req.Items.Select(i =>
                {
                    var material = materials[i.MaterialCode];
                    var item = new WarehouseTransactionItem
                    {
                        MaterialCode = i.MaterialCode,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        ConstructionPhase = i.ConstructionPhase,
                        Notes = i.Notes
                    };
                    TransactionItemHelper.ApplySnapshot(item, material);
                    return item;
                }).ToList()
            };

            db.WarehouseTransactions.Add(transaction);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return MapToDto(transaction, materials);
        }, ct: ct);
    }

    private static TransactionDto MapToDto(WarehouseTransaction t, Dictionary<string, Material> materials)
        => new(
            t.TicketCode, t.TransactionType.ToString(), t.ProjectCode,
            t.SupplierId, null,
            t.InvoiceNumber, t.DnttNumber,
            t.CreatedByName, t.ApprovalStatus.ToString(),
            t.ApprovedByName, t.ApprovedAt, t.Notes, t.CreatedAt,
            t.Items.Select(i => new TransactionItemDto(
                i.Id, i.MaterialCode,
                TransactionItemHelper.ResolveName(i),
                i.Quantity,
                TransactionItemHelper.ResolveUnit(i),
                i.UnitPrice, i.TotalAmount, i.ConstructionPhase, i.Notes)).ToList());
}
