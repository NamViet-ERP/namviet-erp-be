namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed class ImportBudgetExceededException : InvalidOperationException
{
    public ImportBudgetExceededException(
        string projectCode,
        decimal materialBudget,
        decimal approvedImportValue,
        decimal importValue,
        decimal overflowAmount)
        : base($"Duyệt phiếu sẽ vượt ngân sách vật tư của kho dự án {projectCode}.")
    {
        ProjectCode = projectCode;
        MaterialBudget = materialBudget;
        ApprovedImportValue = approvedImportValue;
        ImportValue = importValue;
        OverflowAmount = overflowAmount;
    }

    public string ProjectCode { get; }
    public decimal MaterialBudget { get; }
    public decimal ApprovedImportValue { get; }
    public decimal ImportValue { get; }
    public decimal OverflowAmount { get; }
}
