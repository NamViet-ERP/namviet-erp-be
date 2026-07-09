using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IProjectBudgetService
{
    Task<decimal> GetApprovedImportValueAsync(string projectCode, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, decimal>> GetApprovedImportValuesAsync(
        IReadOnlyList<string> projectCodes,
        CancellationToken ct = default);

    decimal SumImportValue(WarehouseTransaction transaction);

    Task EnsureImportApprovalWithinBudgetAsync(
        WarehouseTransaction transaction,
        bool confirmOverBudget,
        CancellationToken ct = default);
}

public sealed class ProjectBudgetService(WarehouseDbContext db) : IProjectBudgetService
{
    public async Task<decimal> GetApprovedImportValueAsync(string projectCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectCode)) return 0m;

        return await (
            from item in db.WarehouseTransactionItems.AsNoTracking()
            join txn in db.WarehouseTransactions.AsNoTracking() on item.TicketCode equals txn.TicketCode
            where txn.TransactionType == TransactionType.Import
                  && txn.ApprovalStatus == ApprovalStatus.Approved
                  && txn.BeneficiaryProjectCode == projectCode
            select item.Quantity * item.UnitPrice
        ).SumAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetApprovedImportValuesAsync(
        IReadOnlyList<string> projectCodes,
        CancellationToken ct = default)
    {
        if (projectCodes.Count == 0) return new Dictionary<string, decimal>();

        var codes = projectCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count == 0) return new Dictionary<string, decimal>();

        var lines = await (
            from item in db.WarehouseTransactionItems.AsNoTracking()
            join txn in db.WarehouseTransactions.AsNoTracking() on item.TicketCode equals txn.TicketCode
            where txn.TransactionType == TransactionType.Import
                  && txn.ApprovalStatus == ApprovalStatus.Approved
                  && txn.BeneficiaryProjectCode != null
                  && codes.Contains(txn.BeneficiaryProjectCode)
            select new { ProjectCode = txn.BeneficiaryProjectCode!, LineValue = item.Quantity * item.UnitPrice }
        ).ToListAsync(ct);

        return lines
            .GroupBy(x => x.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.LineValue),
                StringComparer.OrdinalIgnoreCase);
    }

    public decimal SumImportValue(WarehouseTransaction transaction) =>
        transaction.Items.Sum(i => i.Quantity * i.UnitPrice);

    public async Task EnsureImportApprovalWithinBudgetAsync(
        WarehouseTransaction transaction,
        bool confirmOverBudget,
        CancellationToken ct = default)
    {
        var projectCode = ResolveProjectCode(transaction);
        if (string.IsNullOrWhiteSpace(projectCode)) return;

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode, ct);

        if (project is null || project.MaterialBudget <= 0) return;

        var approvedValue = await GetApprovedImportValueAsync(projectCode, ct);
        var importValue = SumImportValue(transaction);
        var projectedTotal = approvedValue + importValue;

        if (projectedTotal <= project.MaterialBudget || confirmOverBudget) return;

        throw new ImportBudgetExceededException(
            projectCode,
            project.MaterialBudget,
            approvedValue,
            importValue,
            projectedTotal - project.MaterialBudget);
    }

    private static string? ResolveProjectCode(WarehouseTransaction transaction) =>
        string.IsNullOrWhiteSpace(transaction.BeneficiaryProjectCode)
            ? transaction.ProjectCode
            : transaction.BeneficiaryProjectCode;
}
