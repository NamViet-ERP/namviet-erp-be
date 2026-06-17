namespace NamViet.ERP.Modules.Identity.Domain.Entities;

public class ApplicationRole : Microsoft.AspNetCore.Identity.IdentityRole<Guid>
{
    public string? Description { get; set; }
    public string Module { get; set; } = "General";
    public bool IsActive { get; set; } = true;
}
