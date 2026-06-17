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
    public DbSet<WmsWarehouse> WmsWarehouses => Set<WmsWarehouse>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<Stocktake> Stocktakes => Set<Stocktake>();
    public DbSet<StocktakeItem> StocktakeItems => Set<StocktakeItem>();
    public DbSet<Adjustment> Adjustments => Set<Adjustment>();
    public DbSet<AdjustmentItem> AdjustmentItems => Set<AdjustmentItem>();
    public DbSet<ConsumableQuota> ConsumableQuotas => Set<ConsumableQuota>();
    public DbSet<ProjectBom> ProjectBoms => Set<ProjectBom>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<ExpectedImport> ExpectedImports => Set<ExpectedImport>();
    public DbSet<Conversion> Conversions => Set<Conversion>();
    public DbSet<ConversionItem> ConversionItems => Set<ConversionItem>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<WmsSetting> WmsSettings => Set<WmsSetting>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("warehouse");

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

        model.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasKey(p => p.ProjectCode);
            e.Property(p => p.ProjectCode).HasMaxLength(50);
            e.Property(p => p.ProjectName).HasMaxLength(255).IsRequired();
            e.Property(p => p.ProjectType).HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(p => p.MaterialBudget).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            e.Property(p => p.PmName).HasMaxLength(255);
            e.Property(p => p.ClientProjectId).HasMaxLength(50);
            e.Property(p => p.Stage).HasMaxLength(50);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        model.Entity<ProjectInventory>(e =>
        {
            e.ToTable("project_inventory");
            e.HasKey(pi => pi.Id);
            e.HasIndex(pi => new { pi.ProjectCode, pi.MaterialCode }).IsUnique();
            e.Property(pi => pi.PlannedImportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.PlannedExportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.ActualImportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(pi => pi.ActualExportQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Ignore(pi => pi.ActualInventoryQty);
            e.Ignore(pi => pi.PlannedInventoryQty);
            e.HasOne(pi => pi.Project).WithMany(p => p.Inventories)
             .HasForeignKey(pi => pi.ProjectCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pi => pi.Material).WithMany(m => m.ProjectInventories)
             .HasForeignKey(pi => pi.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

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

        model.Entity<WarehouseTransaction>(e =>
        {
            e.ToTable("warehouse_transactions");
            e.HasKey(t => t.TicketCode);
            e.HasIndex(t => t.Id).IsUnique();
            e.Property(t => t.TicketCode).HasMaxLength(50);
            e.Property(t => t.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(t => t.ApprovalStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(t => t.InvoiceNumber).HasMaxLength(100);
            e.Property(t => t.DnttNumber).HasMaxLength(100);
            e.Property(t => t.CreatedByName).HasMaxLength(255);
            e.Property(t => t.ApprovedByName).HasMaxLength(255);
            e.Property(t => t.WarehouseId).HasMaxLength(50);
            e.Property(t => t.FromWarehouseId).HasMaxLength(50);
            e.Property(t => t.ToWarehouseId).HasMaxLength(50);
            e.Property(t => t.ReferenceCode).HasMaxLength(100);
            e.Property(t => t.SoCode).HasMaxLength(100);
            e.Property(t => t.Customer).HasMaxLength(255);
            e.Property(t => t.WarehouseKeeper).HasMaxLength(255);
            e.Property(t => t.WarehouseApprovalStatus).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(WarehouseKeeperApprovalStatus.Pending);
            e.Property(t => t.BeneficiaryProjectCode).HasMaxLength(50);
            e.Property(t => t.SupplierName).HasMaxLength(255);
            e.HasOne(t => t.Project).WithMany(p => p.Transactions)
             .HasForeignKey(t => t.ProjectCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(t => t.Supplier).WithMany(s => s.Transactions)
             .HasForeignKey(t => t.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

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

        model.Entity<WmsWarehouse>(e =>
        {
            e.ToTable("warehouses");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasMaxLength(50);
            e.Property(w => w.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(w => w.Code).IsUnique();
            e.Property(w => w.Name).HasMaxLength(255).IsRequired();
            e.Property(w => w.Group).HasMaxLength(100);
            e.Property(w => w.Address).HasMaxLength(500);
            e.Property(w => w.Branch).HasMaxLength(100);
            e.Property(w => w.Manager).HasMaxLength(255);
            e.Property(w => w.Status).HasMaxLength(20).HasDefaultValue("active");
            e.HasQueryFilter(w => w.DeletedAt == null);
        });

        model.Entity<WarehouseInventory>(e =>
        {
            e.ToTable("warehouse_inventory");
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.WarehouseId, i.MaterialCode }).IsUnique();
            e.Property(i => i.Qty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.HasOne(i => i.Warehouse).WithMany(w => w.Inventories)
             .HasForeignKey(i => i.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Stocktake>(e =>
        {
            e.ToTable("stocktakes");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(50);
            e.Property(s => s.VoucherCode).HasMaxLength(50);
            e.Property(s => s.CreatedByName).HasMaxLength(255);
            e.HasIndex(s => s.VoucherCode).IsUnique();
            e.HasOne(s => s.Warehouse).WithMany()
             .HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(s => s.DeletedAt == null);
        });

        model.Entity<StocktakeItem>(e =>
        {
            e.ToTable("stocktake_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.HasOne(i => i.Stocktake).WithMany(s => s.Items)
             .HasForeignKey(i => i.StocktakeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Adjustment>(e =>
        {
            e.ToTable("adjustments");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(50);
            e.Property(a => a.VoucherCode).HasMaxLength(50);
            e.Property(a => a.CreatedByName).HasMaxLength(255);
            e.HasIndex(a => a.VoucherCode).IsUnique();
            e.HasOne(a => a.Warehouse).WithMany()
             .HasForeignKey(a => a.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(a => a.DeletedAt == null);
        });

        model.Entity<AdjustmentItem>(e =>
        {
            e.ToTable("adjustment_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.HasOne(i => i.Adjustment).WithMany(a => a.Items)
             .HasForeignKey(i => i.AdjustmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ConsumableQuota>(e =>
        {
            e.ToTable("consumable_quotas");
            e.HasKey(q => q.Id);
            e.HasIndex(q => new { q.ProjectCode, q.MaterialCode }).IsUnique();
            e.Property(q => q.QuotaQty).HasColumnType("numeric(18,4)");
            e.Property(q => q.IssuedQty).HasColumnType("numeric(18,4)");
        });

        model.Entity<ProjectBom>(e =>
        {
            e.ToTable("project_boms");
            e.HasKey(b => b.Id);
            e.HasIndex(b => new { b.ProjectCode, b.MaterialCode }).IsUnique();
            e.Property(b => b.Qty).HasColumnType("numeric(18,4)");
        });

        model.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_orders");
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.ProjectCode).HasMaxLength(50);
            e.HasOne(p => p.Project).WithMany()
             .HasForeignKey(p => p.ProjectCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(p => p.Supplier).WithMany()
             .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        model.Entity<PurchaseOrderItem>(e =>
        {
            e.ToTable("purchase_order_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.HasOne(i => i.PurchaseOrder).WithMany(p => p.Items)
             .HasForeignKey(i => i.PoCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ExpectedImport>(e =>
        {
            e.ToTable("expected_imports");
            e.HasKey(ei => ei.Id);
            e.Property(ei => ei.Id).HasMaxLength(50);
            e.Property(ei => ei.ProjectCode).HasMaxLength(50);
            e.Property(ei => ei.MaterialCode).HasMaxLength(100);
            e.HasOne(ei => ei.Project).WithMany()
             .HasForeignKey(ei => ei.ProjectCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ei => ei.Material).WithMany()
             .HasForeignKey(ei => ei.MaterialCode).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(ei => ei.DeletedAt == null);
        });

        model.Entity<Conversion>(e =>
        {
            e.ToTable("conversions");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(50);
            e.Property(c => c.VoucherCode).HasMaxLength(50);
            e.Property(c => c.CreatedByName).HasMaxLength(255);
            e.Property(c => c.ApprovedByName).HasMaxLength(255);
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        model.Entity<ConversionItem>(e =>
        {
            e.ToTable("conversion_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.HasOne(i => i.Conversion).WithMany(c => c.Items)
             .HasForeignKey(i => i.ConversionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ProductBarcode>(e =>
        {
            e.ToTable("product_barcodes");
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.NumericSku).IsUnique();
            e.HasOne(b => b.Material).WithMany()
             .HasForeignKey(b => b.MaterialCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<WmsSetting>(e =>
        {
            e.ToTable("wms_settings");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Key).HasMaxLength(100);
        });
    }
}
