using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Hr;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Mappers.Hr;

public static class HrEmployeeMapper
{
    public const string PendingCodePrefix = "__PENDING__";
    public const string EmployeeCodePrefix = "NV-";
    public const int EmployeeCodeDigits = 4;

    public static string NewPendingCode() => $"{PendingCodePrefix}{Guid.NewGuid():N}";

    public static string FormatEmployeeCodeNumber(int num) =>
        num.ToString().PadLeft(EmployeeCodeDigits, '0');

    public static string FormatEmployeeCode(int num) =>
        $"{EmployeeCodePrefix}{FormatEmployeeCodeNumber(num)}";

    public static int? ParseEmployeeCodeNumber(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || IsPendingCode(code)) return null;
        var match = Regex.Match(code.Trim(), @"(\d+)$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var n)) return null;
        return n;
    }

    public static string SuggestNextEmployeeCode(IEnumerable<string?> codes)
    {
        var max = 0;
        foreach (var code in codes)
        {
            var n = ParseEmployeeCodeNumber(code);
            if (n.HasValue && n.Value > max) max = n.Value;
        }
        return FormatEmployeeCode(max + 1);
    }

    public static async Task<string> GetSuggestedCodeAsync(
        HrDbContext db, int? excludeId, CancellationToken ct)
    {
        var codes = await db.Employees
            .Where(e => e.DeletedAt == null
                && e.Status == EmployeeStatus.Active
                && (excludeId == null || e.Id != excludeId))
            .Select(e => e.Code)
            .ToListAsync(ct);
        return SuggestNextEmployeeCode(codes);
    }

    public static bool IsPendingCode(string? code) =>
        !string.IsNullOrEmpty(code) &&
        code.StartsWith(PendingCodePrefix, StringComparison.Ordinal);

    public static string? FormatEmployeeCode(string? code) => IsPendingCode(code) ? null : code;

    public static DateOnly ParseDisplayDate(string? value)
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

    public static string FormatDisplayDate(DateOnly d) => d.ToString("dd/MM/yyyy");

    public static string? FormatDisplayDate(DateOnly? d) => d?.ToString("dd/MM/yyyy");

    public static string ToDisplayGender(Gender g) => g == Gender.Female ? "Nữ" : "Nam";

    public static Gender FromDisplayGender(string? value) =>
        value?.Trim() == "Nữ" ? Gender.Female : Gender.Male;

    public static string ToClientStatus(EmployeeStatus s) => s switch
    {
        EmployeeStatus.Resigned => "Nghỉ việc",
        EmployeeStatus.OnLeave => "Thai sản",
        _ => "Đang làm"
    };

    public static EmployeeStatus FromClientStatus(string? value) => value?.Trim() switch
    {
        "Nghỉ việc" => EmployeeStatus.Resigned,
        "Thai sản" => EmployeeStatus.OnLeave,
        _ => EmployeeStatus.Active
    };

    public static string ToDisplayEducation(Education e) => e switch
    {
        Education.College => "Cao đẳng",
        Education.HighSchool => "THPT",
        Education.Other => "Khác",
        _ => "Đại học"
    };

    public static Education FromDisplayEducation(string? value) => value?.Trim() switch
    {
        "Cao đẳng" => Education.College,
        "THPT" => Education.HighSchool,
        "Khác" => Education.Other,
        _ => Education.University
    };

    public static EmployeeDto ToDto(Employee e) => new(
        e.Id.ToString(), FormatEmployeeCode(e.Code), e.FullName,
        ToDisplayGender(e.Gender),
        FormatDisplayDate(e.BirthDate),
        FormatDisplayDate(e.JoinDate),
        FormatDisplayDate(e.OfficialDate),
        ToClientStatus(e.Status),
        FormatDisplayDate(e.ResignDate),
        e.Segment, e.Branch, e.Department?.Name, e.Position?.Name,
        e.Email, e.Phone, e.AddressPermanent, e.AddressCurrent,
        ToDisplayEducation(e.Education), e.School, e.Major,
        e.BankAccountName, e.BankAccount, e.BankName, e.BankBranch,
        e.HadSocialInsurance, e.SiNumber,
        e.Cccd, FormatDisplayDate(e.CccdDate), e.CccdPlace,
        e.CmndNumber, e.CmndIssueDate, e.CmndIssuePlace,
        e.MaritalStatus, e.TaxCode,
        e.PhotoUrl, e.CccdFrontUrl, e.CccdBackUrl, e.DegreePhotoUrl);

    public static async Task ApplyFromDtoAsync(
        Employee e, EmployeeDto dto, HrDbContext db, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            e.FullName = dto.FullName;
        e.Gender = FromDisplayGender(dto.Gender);
        e.BirthDate = ParseDisplayDate(dto.BirthDate);
        e.JoinDate = ParseDisplayDate(dto.JoinDate);
        if (!string.IsNullOrWhiteSpace(dto.OfficialDate))
            e.OfficialDate = ParseDisplayDate(dto.OfficialDate);
        e.Status = FromClientStatus(dto.Status);
        if (e.Status == EmployeeStatus.Resigned)
        {
            e.ResignDate = string.IsNullOrWhiteSpace(dto.ResignDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : ParseDisplayDate(dto.ResignDate);
        }
        else if (e.Status == EmployeeStatus.Active)
            e.ResignDate = null;

        e.Segment = dto.Segment;
        e.Branch = dto.Branch;
        e.Phone = dto.PersonalPhone;
        e.Email = dto.PersonalEmail;
        e.AddressPermanent = dto.PermanentAddress;
        e.AddressCurrent = dto.TemporaryAddress;
        e.Education = FromDisplayEducation(dto.Education);
        e.School = dto.School;
        e.Major = dto.Major;
        e.BankAccountName = dto.BankAccountName;
        e.BankAccount = dto.BankAccountNumber;
        e.BankName = dto.BankName;
        e.BankBranch = dto.BankBranch;
        e.HadSocialInsurance = dto.HadSocialInsurance;
        e.SiNumber = dto.SiNumber;
        e.Cccd = dto.CccdNumber;
        e.CccdDate = string.IsNullOrWhiteSpace(dto.CccdIssueDate) ? null : ParseDisplayDate(dto.CccdIssueDate);
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
