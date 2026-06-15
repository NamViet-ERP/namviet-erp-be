using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Mappers.Hr;

public static class HrEmployeeMapper
{
    public const string PendingCodePrefix = "__PENDING__";

    public static string NewPendingCode() => $"{PendingCodePrefix}{Guid.NewGuid():N}";

    public static bool IsPendingCode(string? code) =>
        !string.IsNullOrEmpty(code) &&
        code.StartsWith(PendingCodePrefix, StringComparison.Ordinal);

    public static string? ToFeCode(string? code) => IsPendingCode(code) ? null : code;

    public static DateOnly ParseFeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateOnly.FromDateTime(DateTime.UtcNow);
        if (DateOnly.TryParseExact(value, "dd/MM/yyyy", out var d))
            return d;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", out d))
            return d;
        if (DateOnly.TryParse(value, out d))
            return d;
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public static string FormatFeDate(DateOnly d) => d.ToString("dd/MM/yyyy");

    public static string FormatFeDate(DateOnly? d) => d?.ToString("dd/MM/yyyy");

    public static string ToFeGender(Gender g) => g == Gender.Female ? "Nữ" : "Nam";

    public static Gender FromFeGender(string? value) =>
        value?.Trim() == "Nữ" ? Gender.Female : Gender.Male;

    public static string ToClientStatus(EmployeeStatus s) => s switch
    {
        EmployeeStatus.Resigned => "Nghỉ việc",
        EmployeeStatus.OnLeave => "Thai sản",
        _ => "Đang làm"
    };

    public static EmployeeStatus FromFeStatus(string? value) => value?.Trim() switch
    {
        "Nghỉ việc" => EmployeeStatus.Resigned,
        "Thai sản" => EmployeeStatus.OnLeave,
        _ => EmployeeStatus.Active
    };

    public static string ToFeEducation(Education e) => e switch
    {
        Education.College => "Cao đẳng",
        Education.HighSchool => "THPT",
        Education.Other => "Khác",
        _ => "Đại học"
    };

    public static Education FromFeEducation(string? value) => value?.Trim() switch
    {
        "Cao đẳng" => Education.College,
        "THPT" => Education.HighSchool,
        "Khác" => Education.Other,
        _ => Education.University
    };

    public static FeEmployeeDto ToDto(Employee e) => new(
        e.Id.ToString(), ToFeCode(e.Code), e.FullName,
        ToFeGender(e.Gender),
        FormatFeDate(e.BirthDate),
        FormatFeDate(e.JoinDate),
        FormatFeDate(e.OfficialDate),
        ToClientStatus(e.Status),
        FormatFeDate(e.ResignDate),
        e.Segment, e.Branch, e.Department?.Name, e.Position?.Name,
        e.Email, e.Phone, e.AddressPermanent, e.AddressCurrent,
        ToFeEducation(e.Education), e.School, e.Major,
        e.BankAccountName, e.BankAccount, e.BankName, e.BankBranch,
        e.HadSocialInsurance, e.SiNumber,
        e.Cccd, FormatFeDate(e.CccdDate), e.CccdPlace,
        e.CmndNumber, e.CmndIssueDate, e.CmndIssuePlace,
        e.MaritalStatus, e.TaxCode,
        e.PhotoUrl, e.CccdFrontUrl, e.CccdBackUrl, e.DegreePhotoUrl);

    public static async Task ApplyFromDtoAsync(
        Employee e, FeEmployeeDto dto, HrDbContext db, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            e.FullName = dto.FullName;
        e.Gender = FromFeGender(dto.Gender);
        e.BirthDate = ParseFeDate(dto.BirthDate);
        e.JoinDate = ParseFeDate(dto.JoinDate);
        if (!string.IsNullOrWhiteSpace(dto.OfficialDate))
            e.OfficialDate = ParseFeDate(dto.OfficialDate);
        e.Status = FromFeStatus(dto.Status);
        if (e.Status == EmployeeStatus.Resigned)
        {
            e.ResignDate = string.IsNullOrWhiteSpace(dto.ResignDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : ParseFeDate(dto.ResignDate);
        }
        else if (e.Status == EmployeeStatus.Active)
            e.ResignDate = null;

        e.Segment = dto.Segment;
        e.Branch = dto.Branch;
        e.Phone = dto.PersonalPhone;
        e.Email = dto.PersonalEmail;
        e.AddressPermanent = dto.PermanentAddress;
        e.AddressCurrent = dto.TemporaryAddress;
        e.Education = FromFeEducation(dto.Education);
        e.School = dto.School;
        e.Major = dto.Major;
        e.BankAccountName = dto.BankAccountName;
        e.BankAccount = dto.BankAccountNumber;
        e.BankName = dto.BankName;
        e.BankBranch = dto.BankBranch;
        e.HadSocialInsurance = dto.HadSocialInsurance;
        e.SiNumber = dto.SiNumber;
        e.Cccd = dto.CccdNumber;
        e.CccdDate = string.IsNullOrWhiteSpace(dto.CccdIssueDate) ? null : ParseFeDate(dto.CccdIssueDate);
        e.CccdPlace = dto.CccdIssuePlace;
        e.CmndNumber = dto.CmndNumber;
        e.CmndIssueDate = dto.CmndIssueDate;
        e.CmndIssuePlace = dto.CmndIssuePlace;
        e.MaritalStatus = dto.MaritalStatus;
        e.TaxCode = dto.TaxCode;
        if (!string.IsNullOrWhiteSpace(dto.PhotoUrl)) e.PhotoUrl = dto.PhotoUrl;
        if (!string.IsNullOrWhiteSpace(dto.CccdFrontUrl)) e.CccdFrontUrl = dto.CccdFrontUrl;
        if (!string.IsNullOrWhiteSpace(dto.CccdBackUrl)) e.CccdBackUrl = dto.CccdBackUrl;
        if (!string.IsNullOrWhiteSpace(dto.DegreePhotoUrl)) e.DegreePhotoUrl = dto.DegreePhotoUrl;

        if (!string.IsNullOrWhiteSpace(dto.Department))
        {
            var dept = await db.Departments.FirstOrDefaultAsync(d => d.Name == dto.Department, ct);
            if (dept is not null) e.DepartmentId = dept.Id;
        }

        if (!string.IsNullOrWhiteSpace(dto.Position))
        {
            var pos = await db.Positions.FirstOrDefaultAsync(p => p.Name == dto.Position, ct);
            if (pos is not null) e.PositionId = pos.Id;
        }
    }

    public static async Task EnsureDefaultDepartmentAsync(Employee e, HrDbContext db, CancellationToken ct)
    {
        if (e.DepartmentId > 0) return;
        var dept = await db.Departments.FirstOrDefaultAsync(ct)
            ?? new Department { Code = "DEFAULT", Name = "Mặc định" };
        if (dept.Id == 0)
        {
            db.Departments.Add(dept);
            await db.SaveChangesAsync(ct);
        }
        e.DepartmentId = dept.Id;
    }

    public static async Task<bool> IsCodeTakenAsync(
        HrDbContext db, string? code, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = code.Trim().ToUpperInvariant();
        return await db.Employees.AnyAsync(
            x => x.DeletedAt == null && x.Code.ToUpper() == normalized && x.Id != excludeId, ct);
    }
}
