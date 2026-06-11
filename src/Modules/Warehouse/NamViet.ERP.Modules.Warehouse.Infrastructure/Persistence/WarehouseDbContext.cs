using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

public class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options)
{
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PriceBook> PriceBooks => Set<PriceBook>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectInventory> ProjectInventories => Set<ProjectInventory>();
    public DbSet<ProjectMaterialLoan> ProjectMaterialLoans => Set<ProjectMaterialLoan>();
    public DbSet<WarehouseTransaction> WarehouseTransactions => Set<WarehouseTransaction>();
    public DbSet<WarehouseTransactionItem> WarehouseTransactionItems => Set<WarehouseTransactionItem>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("warehouse");

        // ── materials ──────────────────────────────────────────────────────
        model.Entity<Material>(e =>
        {
            e.ToTable("materials");
            e.HasKey(m => m.MaterialCode);
            e.Property(m => m.MaterialCode).HasMaxLength(100);
            e.Property(m => m.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(m => m.MaterialCategory).HasMaxLength(50);
            e.Property(m => m.Unit).HasMaxLength(50).IsRequired();
            e.HasQueryFilter(m => m.DeletedAt == null);
        });

        // ── suppliers ──────────────────────────────────────────────────────
        model.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasKey(s => s.Id);
            e.Property(s => s.SupplierCode).HasMaxLength(50).IsRequired();
            e.HasIndex(s => s.SupplierCode).IsUnique();
            e.Property(s => s.SupplierName).HasMaxLength(255).IsRequired();
            e.Property(s => s.ContactPhone).HasMaxLength(50);
            e.Property(s => s.Rating).HasMaxLength(10).HasDefaultValue("A");
            e.HasQueryFilter(s => s.DeletedAt == null);
        });

        // ── price_book ─────────────────────────────────────────────────────
        model.Entity<PriceBook>(e =>
        {
            e.ToTable("price_book");
            e.HasKey(p => p.Id);
            e.Property(p => p.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();
            e.HasOne(p => p.Material).WithMany(m => m.PriceBooks)
             .HasForeignKey(p => p.MaterialCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Supplier).WithMany(s => s.PriceBooks)
             .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── projects ───────────────────────────────────────────────────────
        model.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasKey(p => p.ProjectCode);
            e.Property(p => p.ProjectCode).HasMaxLength(50);
            e.Property(p => p.ProjectName).HasMaxLength(255).IsRequired();
            e.Property(p => p.ProjectType)
             .HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(p => p.Status)
             .HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(p => p.MaterialBudget).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            e.Property(p => p.PmName).HasMaxLength(255);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        // ── project_inventory ──────────────────────────────────────────────
        model.Entity<ProjectInventory>(e =>
        {
            e.ToTable("project_inventory");
            e.HasKey(pi => pi.Id);
            e.HasIndex(pi => new { pi.ProjectCode, pi.MaterialCode }).IsUnique();
            e.Property(pi => pi.PlannedImportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.PlannedExportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.ActualImportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.ActualExportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            // Computed columns – không map sang DB, chỉ tính ở C#
            e.Ignore(pi => pi.ActualInventoryQty);
            e.Ignore(pi => pi.PlannedInventoryQty);
            e.HasOne(pi => pi.Project).WithMany(p => p.Inventories)
             .HasForeignKey(pi => pi.ProjectCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pi => pi.Material).WithMany(m => m.ProjectInventories)
             .HasForeignKey(pi => pi.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        // ── project_material_loans ─────────────────────────────────────────
        model.Entity<ProjectMaterialLoan>(e =>
        {
            e.ToTable("project_material_loans");
            e.HasKey(l => l.LoanCode);
            e.Property(l => l.LoanCode).HasMaxLength(50);
            e.Property(l => l.LoanQty).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(l => l.ReturnTicketLink).HasMaxLength(50);
            e.HasOne(l => l.BorrowingProject).WithMany()
             .HasForeignKey(l => l.BorrowingProjectCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.LendingProject).WithMany()
             .HasForeignKey(l => l.LendingProjectCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Material).WithMany()
             .HasForeignKey(l => l.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        // ── warehouse_transactions ─────────────────────────────────────────
        model.Entity<WarehouseTransaction>(e =>
        {
            e.ToTable("warehouse_transactions");
            e.HasKey(t => t.TicketCode);
            e.Property(t => t.TicketCode).HasMaxLength(50);
            e.Property(t => t.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(t => t.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(t => t.InvoiceNumber).HasMaxLength(100);
            e.Property(t => t.DnttNumber).HasMaxLength(100);
            e.Property(t => t.CreatedByName).HasMaxLength(255);
            e.Property(t => t.ApprovedByName).HasMaxLength(255);
            e.HasOne(t => t.Project).WithMany(p => p.Transactions)
             .HasForeignKey(t => t.ProjectCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(t => t.Supplier).WithMany(s => s.Transactions)
             .HasForeignKey(t => t.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // ── warehouse_transaction_items ────────────────────────────────────
        model.Entity<WarehouseTransactionItem>(e =>
        {
            e.ToTable("warehouse_transaction_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Quantity).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(i => i.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();
            e.Property(i => i.ConstructionPhase).HasMaxLength(255);
            e.Ignore(i => i.TotalAmount);
            e.HasOne(i => i.Transaction).WithMany(t => t.Items)
             .HasForeignKey(i => i.TicketCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany(m => m.TransactionItems)
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
