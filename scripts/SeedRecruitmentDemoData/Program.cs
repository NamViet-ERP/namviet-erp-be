using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

const string SeedEmailPrefix = "demo.recruitment.";
const string SeedPlanReason = "[seed-demo]";
const string SeedCostNote = "[seed-demo]";

var force = args.Any(a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase));

var apiConfigDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiHost", "NamViet.ERP.Api"));
var configuration = new ConfigurationBuilder()
    .SetBasePath(apiConfigDir)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets("namviet-erp-api")
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Thiếu ConnectionStrings:Default.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddHrModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

await db.Database.MigrateAsync();

if (!force && await db.Candidates.AnyAsync(c => c.Email.StartsWith(SeedEmailPrefix) && c.DeletedAt == null))
{
    Console.WriteLine("Dữ liệu demo tuyển dụng đã có — bỏ qua. Dùng --force để seed lại.");
    return 0;
}

if (force)
{
    var demoCandidates = await db.Candidates
        .Where(c => c.Email.StartsWith(SeedEmailPrefix))
        .ToListAsync();
    foreach (var c in demoCandidates)
        c.DeletedAt = DateTime.UtcNow;

    var demoPlans = await db.RecruitmentPlans
        .Where(p => p.Reason == SeedPlanReason)
        .ToListAsync();
    db.RecruitmentPlans.RemoveRange(demoPlans);

    var demoCosts = await db.RecruitmentCosts
        .Where(c => c.Note == SeedCostNote)
        .ToListAsync();
    foreach (var c in demoCosts)
        c.DeletedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
}

var positions = await db.RecruitmentPositions
    .AsNoTracking()
    .Where(p => p.DeletedAt == null && p.IsActive)
    .OrderBy(p => p.Id)
    .ToListAsync();
if (positions.Count == 0)
{
    Console.Error.WriteLine("Chưa có vị trí tuyển dụng. Tạo ít nhất 1 vị trí trong Thiết lập tuyển dụng trước.");
    return 1;
}

var department = await db.Departments
    .AsNoTracking()
    .Where(d => d.DeletedAt == null)
    .OrderBy(d => d.Id)
    .FirstOrDefaultAsync();
if (department is null)
{
    Console.Error.WriteLine("Chưa có phòng ban trong HR. Cần ít nhất 1 phòng ban.");
    return 1;
}

var hrResponsible = await db.Employees
    .AsNoTracking()
    .Where(e => e.DeletedAt == null && e.Status == EmployeeStatus.Active)
    .OrderBy(e => e.Id)
    .Select(e => e.FullName)
    .FirstOrDefaultAsync() ?? "System Admin";

var posA = positions[0];
var posB = positions.Count > 1 ? positions[1] : positions[0];
var now = DateTime.UtcNow;
var year = DateTime.UtcNow.Year;

var candidateSeeds = new[]
{
    SeedRow("01", "Nguyễn Văn An", CandidateStatus.New, RecruitmentSource.Facebook, "Facebook", posA, -1, null, null),
    SeedRow("02", "Trần Thị Bình", CandidateStatus.Screening, RecruitmentSource.TopCV, "TopCV", posA, -3, null, null),
    SeedRow("03", "Lê Minh Cường", CandidateStatus.InterviewScheduled, RecruitmentSource.JobPortal24h, "24h - ứng tuyển", posB, -5, "15/07/2026", "09:30"),
    SeedRow("04", "Phạm Thu Dung", CandidateStatus.Interview1, RecruitmentSource.Glints, "Glints", posB, -7, "12/07/2026", "14:00"),
    SeedRow("05", "Hoàng Văn Em", CandidateStatus.InterviewPassed, RecruitmentSource.Referral, "Giới thiệu", posA, -10, "05/07/2026", "10:00"),
    SeedRow("06", "Võ Thị Phương", CandidateStatus.Offered, RecruitmentSource.Zalo, "Zalo", posA, -12, null, null, offerSalary: "18.000.000"),
    SeedRow("07", "Đặng Quốc Gia", CandidateStatus.Accepted, RecruitmentSource.Internal, "Nội bộ", posB, -20, null, null, startDate: "01/08/2026"),
    SeedRow("08", "Bùi Văn Hiếu", CandidateStatus.Rejected, RecruitmentSource.Facebook, "Facebook", posB, -4, "08/07/2026", "15:00"),
};

foreach (var row in candidateSeeds)
{
    var appliedAt = now.AddDays(row.AppliedDaysAgo);
    var workflow = BuildWorkflow(row, posA, posB, hrResponsible, appliedAt);
    var entity = new Candidate
    {
        FullName = row.FullName,
        Phone = $"09{row.Suffix}12345",
        Email = $"{SeedEmailPrefix}{row.Suffix}@namviet.vn",
        Gender = row.Suffix is "02" or "04" or "06" ? Gender.Female : Gender.Male,
        BirthDate = new DateOnly(1995, int.Parse(row.Suffix), 10),
        Address = "Quận 1, TP. Hồ Chí Minh",
        Education = Education.University,
        PositionId = row.Position.Id,
        Experience = "2 năm",
        Source = row.SourceEnum,
        Status = row.Status,
        AppliedAt = appliedAt,
        CvFileName = $"CV_{row.Suffix}.pdf",
        WorkflowData = JsonSerializer.Serialize(workflow, jsonOptions),
    };
    db.Candidates.Add(entity);
}

db.RecruitmentPlans.AddRange(
    new RecruitmentPlan
    {
        PositionId = posA.Id,
        DepartmentId = department.Id,
        Headcount = 3,
        Reason = SeedPlanReason,
        Period = $"Q3-{year}",
        OpenDate = new DateOnly(year, 7, 1),
        DeadlineDate = new DateOnly(year, 9, 30),
        Status = "Active",
    },
    new RecruitmentPlan
    {
        PositionId = posB.Id,
        DepartmentId = department.Id,
        Headcount = 2,
        Reason = SeedPlanReason,
        Period = $"Q3-{year}",
        OpenDate = new DateOnly(year, 7, 1),
        DeadlineDate = new DateOnly(year, 12, 31),
        Status = "Active",
    });

db.RecruitmentCosts.AddRange(
    new RecruitmentCost { Source = "Facebook", Amount = 5_000_000, Period = $"07/{year}", PositionName = posA.Name, DurationDays = 30, PaymentYear = year, PaymentMonth = 7, Note = SeedCostNote },
    new RecruitmentCost { Source = "TopCV", Amount = 3_200_000, Period = $"07/{year}", PositionName = posB.Name, DurationDays = 14, PaymentYear = year, PaymentMonth = 7, Note = SeedCostNote },
    new RecruitmentCost { Source = "24h - ứng tuyển", Amount = 1_500_000, Period = $"06/{year}", PositionName = posA.Name, DurationDays = 7, PaymentYear = year, PaymentMonth = 6, Note = SeedCostNote },
    new RecruitmentCost { Source = "Glints", Amount = 2_800_000, Period = $"07/{year}", PositionName = posB.Name, DurationDays = 21, PaymentYear = year, PaymentMonth = 7, Note = SeedCostNote });

var budget = await db.RecruitmentBudgets.FirstOrDefaultAsync(b => b.Year == year && b.DeletedAt == null);
if (budget is null)
{
    db.RecruitmentBudgets.Add(new RecruitmentBudget { Year = year, TotalAllocated = 1_000_000_000m });
}
else
{
    budget.TotalAllocated = 1_000_000_000m;
    budget.UpdatedAt = DateTime.UtcNow;
}

await db.SaveChangesAsync();

Console.WriteLine();
Console.WriteLine("Đã seed dữ liệu demo tuyển dụng:");
Console.WriteLine($"  • {candidateSeeds.Length} ứng viên (email: {SeedEmailPrefix}*@namviet.vn)");
Console.WriteLine("  • 2 kế hoạch tuyển dụng (Active)");
Console.WriteLine("  • 4 dòng chi phí + ngân sách năm {0}", year);
Console.WriteLine("  • 1 ứng viên có lịch PV 15/07/2026 (Lịch PV)");
return 0;

static object BuildWorkflow(
    CandidateSeed row,
    RecruitmentPosition posA,
    RecruitmentPosition posB,
    string hrResponsible,
    DateTime appliedAt)
{
    var positionName = row.Position.Name;
    var status = row.Status.ToString();
    var appliedText = appliedAt.ToString("dd/MM/yyyy HH:mm");

    return new Dictionary<string, object?>
    {
        ["id"] = "",
        ["fullName"] = row.FullName,
        ["phone"] = $"09{row.Suffix}12345",
        ["email"] = $"demo.recruitment.{row.Suffix}@namviet.vn",
        ["gender"] = row.Suffix is "02" or "04" or "06" ? "Nữ" : "Nam",
        ["address"] = "Quận 1, TP. Hồ Chí Minh",
        ["birthDate"] = $"10/{row.Suffix.PadLeft(2, '0')}/1995",
        ["education"] = "Đại học",
        ["school"] = "Đại học Kinh tế TP.HCM",
        ["major"] = "Quản trị kinh doanh",
        ["position"] = positionName,
        ["experience"] = "2 năm",
        ["source"] = row.SourceLabel,
        ["hrResponsible"] = hrResponsible,
        ["cvFileName"] = $"CV_{row.Suffix}.pdf",
        ["cvFileSize"] = "128 KB",
        ["appliedAt"] = appliedText,
        ["status"] = status,
        ["interviewDate"] = row.InterviewDate,
        ["interviewTime"] = row.InterviewTime,
        ["evaluator"] = row.InterviewDate is not null ? hrResponsible : null,
        ["evaluationResult"] = row.Status is CandidateStatus.InterviewPassed ? "Đạt"
            : row.Status is CandidateStatus.Rejected ? "Không đạt" : "",
        ["offerStatus"] = row.Status is CandidateStatus.Offered or CandidateStatus.Accepted ? "Đồng ý" : "",
        ["offerSalary"] = row.OfferSalary,
        ["startDate"] = row.StartDate,
        ["screeningType"] = row.Status is CandidateStatus.Screening ? "Lọc CV" : null,
    };
}

static CandidateSeed SeedRow(
    string suffix,
    string fullName,
    CandidateStatus status,
    RecruitmentSource sourceEnum,
    string sourceLabel,
    RecruitmentPosition position,
    int appliedDaysAgo,
    string? interviewDate,
    string? interviewTime,
    string? offerSalary = null,
    string? startDate = null) =>
    new(suffix, fullName, status, sourceEnum, sourceLabel, position, appliedDaysAgo, interviewDate, interviewTime, offerSalary, startDate);

file record CandidateSeed(
    string Suffix,
    string FullName,
    CandidateStatus Status,
    RecruitmentSource SourceEnum,
    string SourceLabel,
    RecruitmentPosition Position,
    int AppliedDaysAgo,
    string? InterviewDate,
    string? InterviewTime,
    string? OfferSalary = null,
    string? StartDate = null);
