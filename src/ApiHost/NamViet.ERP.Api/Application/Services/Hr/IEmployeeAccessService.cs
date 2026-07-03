using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Hr;

public interface IEmployeeAccessService
{
    Task<int?> ResolveLinkedEmployeeIdAsync(ICurrentUser user, CancellationToken ct = default);

    Task<bool> CanViewEmployeeAsync(ICurrentUser user, int employeeId, CancellationToken ct = default);
}
