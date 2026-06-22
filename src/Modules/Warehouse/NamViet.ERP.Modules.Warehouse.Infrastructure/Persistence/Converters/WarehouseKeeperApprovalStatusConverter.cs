using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Converters;

/// <summary>Maps legacy/unknown DB strings to a safe enum value instead of crashing reads.</summary>
public sealed class WarehouseKeeperApprovalStatusConverter
    : ValueConverter<WarehouseKeeperApprovalStatus, string>
{
    private static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(WarehouseKeeperApprovalStatus.Pending),
        nameof(WarehouseKeeperApprovalStatus.Approved),
        nameof(WarehouseKeeperApprovalStatus.Rejected),
    };

    public WarehouseKeeperApprovalStatusConverter()
        : base(
            status => status.ToString(),
            value => Parse(value))
    {
    }

    private static WarehouseKeeperApprovalStatus Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return WarehouseKeeperApprovalStatus.Pending;

        if (Valid.Contains(value))
            return Enum.Parse<WarehouseKeeperApprovalStatus>(value, ignoreCase: true);

        // Legacy demo/export rows used invalid labels like "Exported".
        if (value.Equals("Exported", StringComparison.OrdinalIgnoreCase))
            return WarehouseKeeperApprovalStatus.Approved;

        return WarehouseKeeperApprovalStatus.Pending;
    }
}
