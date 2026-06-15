using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;

namespace NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LaborContract> LaborContracts => Set<LaborContract>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<HrTransfer> HrTransfers => Set<HrTransfer>();
    public DbSet<RecruitmentPosition> RecruitmentPositions => Set<RecruitmentPosition>();
    public DbSet<RecruitmentPlan> RecruitmentPlans => Set<RecruitmentPlan>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateStage> CandidateStages => Set<CandidateStage>();
    public DbSet<Onboard> Onboards => Set<Onboard>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<AttendanceSummary> AttendanceSummaries => Set<AttendanceSummary>();
    public DbSet<SocialInsuranceRecord> SocialInsuranceRecords => Set<SocialInsuranceRecord>();
    public DbSet<SocialInsuranceTask> SocialInsuranceTasks => Set<SocialInsuranceTask>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<KpiDefinition> KpiDefinitions => Set<KpiDefinition>();
    public DbSet<KpiRecord> KpiRecords => Set<KpiRecord>();
    public DbSet<AdminRequest> AdminRequests => Set<AdminRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("hr");

        model.Entity<Department>(e =>
        {
            e.ToTable("departments");
            e.HasKey(d => d.Id);
            e.Property(d => d.Code).HasMaxLength(50).IsRequired();
            e.Property(d => d.Name).HasMaxLength(255).IsRequired();
            e.HasMany(d => d.Children).WithOne(d => d.Parent).HasForeignKey(d => d.ParentId);
            e.HasQueryFilter(d => d.DeletedAt == null);
        });

        model.Entity<Position>(e =>
        {
            e.ToTable("positions");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(255).IsRequired();
            e.HasOne(p => p.Department).WithMany(d => d.Positions).HasForeignKey(p => p.DepartmentId);
        });

        model.Entity<Employee>(e =>
        {
            e.ToTable("employees");
            e.HasKey(em => em.Id);
            e.Property(em => em.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(em => em.Code).IsUnique();
            e.Property(em => em.FullName).HasMaxLength(255).IsRequired();
            e.Property(em => em.Gender).HasConversion<string>().HasMaxLength(10);
            e.Property(em => em.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(em => em.Education).HasConversion<string>().HasMaxLength(20);
            e.Property(em => em.Segment).HasMaxLength(100);
            e.Property(em => em.Branch).HasMaxLength(100);
            e.Property(em => em.MaritalStatus).HasMaxLength(50);
            e.Property(em => em.CmndNumber).HasMaxLength(50);
            e.Property(em => em.School).HasMaxLength(255);
            e.Property(em => em.Major).HasMaxLength(255);
            e.Property(em => em.BankAccountName).HasMaxLength(255);
            e.Property(em => em.BankBranch).HasMaxLength(255);
            e.Property(em => em.HadSocialInsurance).HasMaxLength(50);
            e.HasOne(em => em.Department).WithMany(d => d.Employees).HasForeignKey(em => em.DepartmentId);
            e.HasOne(em => em.Position).WithMany(p => p.Employees).HasForeignKey(em => em.PositionId).IsRequired(false);
            e.HasOne(em => em.Manager).WithMany().HasForeignKey(em => em.ManagerId).IsRequired(false);
            e.HasQueryFilter(em => em.DeletedAt == null);
        });

        model.Entity<LaborContract>(e =>
        {
            e.ToTable("labor_contracts");
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Salary).HasColumnType("numeric(18,2)");
            e.Property(c => c.Allowance).HasColumnType("numeric(18,2)");
            e.HasOne(c => c.Employee).WithMany(em => em.Contracts).HasForeignKey(c => c.EmployeeId);
        });

        model.Entity<Appointment>(e => { e.ToTable("appointments"); e.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId); });
        model.Entity<HrTransfer>(e => { e.ToTable("hr_transfers"); e.HasOne(t => t.Employee).WithMany().HasForeignKey(t => t.EmployeeId); });

        model.Entity<RecruitmentPosition>(e =>
        {
            e.ToTable("recruitment_positions");
            e.HasQueryFilter(r => r.DeletedAt == null);
        });

        model.Entity<RecruitmentPlan>(e => { e.ToTable("recruitment_plans"); });

        model.Entity<Candidate>(e =>
        {
            e.ToTable("candidates");
            e.Property(c => c.Gender).HasConversion<string>().HasMaxLength(10);
            e.Property(c => c.Education).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Source).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.WorkflowData).HasColumnType("jsonb");
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        model.Entity<CandidateStage>(e => { e.ToTable("candidate_stages"); e.HasOne(s => s.Candidate).WithMany(c => c.Stages).HasForeignKey(s => s.CandidateId); });
        model.Entity<Onboard>(e => { e.ToTable("onboards"); e.HasOne(o => o.Employee).WithMany().HasForeignKey(o => o.EmployeeId); });

        model.Entity<LeaveType>(e => { e.ToTable("leave_types"); });
        model.Entity<LeaveRequest>(e =>
        {
            e.ToTable("leave_requests");
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Days).HasColumnType("numeric(5,2)");
            e.HasOne(l => l.Employee).WithMany(em => em.LeaveRequests).HasForeignKey(l => l.EmployeeId);
            e.HasOne(l => l.LeaveType).WithMany().HasForeignKey(l => l.LeaveTypeId);
        });

        model.Entity<LeaveBalance>(e =>
        {
            e.ToTable("leave_balances");
            e.HasIndex(l => new { l.EmployeeId, l.Year }).IsUnique();
            e.Property(l => l.TotalDays).HasColumnType("numeric(5,2)");
            e.Property(l => l.UsedDays).HasColumnType("numeric(5,2)");
            e.Ignore(l => l.RemainingDays);
        });

        model.Entity<Attendance>(e =>
        {
            e.ToTable("attendances");
            e.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
        });

        model.Entity<AttendanceSummary>(e =>
        {
            e.ToTable("attendance_summaries");
            e.HasIndex(a => new { a.EmployeeId, a.Month, a.Year }).IsUnique();
            e.Property(a => a.WorkDays).HasColumnType("numeric(5,2)");
            e.Property(a => a.LeaveDays).HasColumnType("numeric(5,2)");
            e.Property(a => a.OtHours).HasColumnType("numeric(6,2)");
            e.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId);
        });

        model.Entity<SocialInsuranceRecord>(e =>
        {
            e.ToTable("social_insurance_records");
            e.Property(r => r.SalaryBase).HasColumnType("numeric(18,2)");
            e.Property(r => r.EmployeeAmount).HasColumnType("numeric(18,2)");
            e.Property(r => r.CompanyAmount).HasColumnType("numeric(18,2)");
            e.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId);
        });

        model.Entity<SocialInsuranceTask>(e =>
        {
            e.ToTable("social_insurance_tasks");
            e.HasOne(t => t.Employee).WithMany().HasForeignKey(t => t.EmployeeId);
            e.HasOne(t => t.Assignee).WithMany().HasForeignKey(t => t.AssigneeId).IsRequired(false);
        });
        model.Entity<Reward>(e =>
        {
            e.ToTable("rewards");
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(r => r.Amount).HasColumnType("numeric(18,2)");
            e.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId);
        });
        model.Entity<KpiDefinition>(e => { e.ToTable("kpi_definitions"); e.Property(k => k.Target).HasColumnType("numeric(18,4)"); });
        model.Entity<KpiRecord>(e =>
        {
            e.ToTable("kpi_records");
            e.Property(k => k.TargetValue).HasColumnType("numeric(18,4)");
            e.Property(k => k.ActualValue).HasColumnType("numeric(18,4)");
            e.HasOne(k => k.Kpi).WithMany().HasForeignKey(k => k.KpiId);
        });
        model.Entity<AdminRequest>(e =>
        {
            e.ToTable("admin_requests");
            e.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.Quantity).HasColumnType("numeric(10,2)");
            e.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId);
        });
        model.Entity<Notification>(e => { e.ToTable("notifications"); });
    }
}
