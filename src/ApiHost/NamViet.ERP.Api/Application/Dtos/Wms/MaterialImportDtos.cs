namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record MaterialImportRowDto(
    int RowNumber,
    string MaterialCode,
    string MaterialName,
    string Unit,
    string? DanhMuc,
    string? NhomVatTu,
    string? LoaiVatTu,
    string? PhanLoai,
    string? Kieu,
    string? Origin,
    string? TechnicalSpec,
    string? GeneralTechnicalSpec,
    string? QualityStandard,
    decimal? CeilingPrice,
    string? AbcClass,
    bool IsConsumable,
    bool IsValid,
    bool IsExisting,
    bool IsNewCategory,
    List<string> Errors,
    List<string> Warnings);

public record MaterialImportErrorDto(int RowNumber, string Field, string Message);

public record MaterialImportParseResultDto(
    int TotalRows,
    int ValidRowCount,
    int ExistingRowCount,
    int ErrorRowCount,
    List<MaterialImportRowDto> Rows,
    List<MaterialImportErrorDto> Errors);

public record MaterialImportBatchRequest(
    List<MaterialImportRowDto> Rows,
    bool OverwriteExisting = false);

public record MaterialImportResultDto(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int TotalProcessed,
    List<string> ImportedCodes,
    List<string> Errors);
