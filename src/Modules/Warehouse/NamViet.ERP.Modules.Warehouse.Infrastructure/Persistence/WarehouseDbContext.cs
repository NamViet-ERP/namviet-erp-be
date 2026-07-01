using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Converters;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

public class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại và thử lại.",
                ex);
        }
    }

    public override int SaveChanges()
    {
        try
        {
            return base.SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại và thử lại.",
                ex);
        }
    }

    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialCategory> MaterialCategories => Set<MaterialCategory>();
    public DbSet<MaterialUnit> MaterialUnits => Set<MaterialUnit>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
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
    public DbSet<ConsumableQuota> ConsumableQuotas => Set<ConsumableQuota>();
    public DbSet<ProjectBom> ProjectBoms => Set<ProjectBom>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Conversion> Conversions => Set<Conversion>();
    public DbSet<ConversionItem> ConversionItems => Set<ConversionItem>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<WmsSetting> WmsSettings => Set<WmsSetting>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("warehouse");

        model.Entity<MaterialCategory>(e =>
        {
            e.ToTable("material_categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Name).HasMaxLength(255).IsRequired();
            e.Property(c => c.Level).IsRequired();
            e.HasIndex(c => c.ParentId);
            e.HasOne(c => c.Parent).WithMany(c => c.Children)
             .HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<MaterialUnit>(e =>
        {
            e.ToTable("material_units");
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(u => u.Name).IsUnique();
            e.Property(u => u.IsActive).HasDefaultValue(true);
        });

        model.Entity<Material>(e =>
        {
            e.ToTable("materials");
            e.HasKey(m => m.MaterialCode);
            e.Property(m => m.MaterialCode).HasMaxLength(100);
            e.Property(m => m.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(m => m.Unit).HasMaxLength(50).IsRequired();
            e.Property(m => m.IsActive).HasDefaultValue(true);
            e.HasOne(m => m.Category).WithMany(c => c.Materials)
             .HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(m => m.DeletedAt == null);
        });

        model.Entity<InventoryReservation>(e =>
        {
            e.ToTable("inventory_reservations");
            e.HasKey(r => r.Id);
            e.Property(r => r.WarehouseId).HasMaxLength(50).IsRequired();
            e.Property(r => r.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(r => r.MaterialCode).HasMaxLength(100).IsRequired();
            e.Property(r => r.SourceTicketCode).HasMaxLength(50).IsRequired();
            e.Property(r => r.ReservedQty).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(r => r.ExternalQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(r => r.FulfilledQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(r => new { r.ProjectCode, r.MaterialCode });
            e.HasIndex(r => new { r.WarehouseId, r.MaterialCode, r.Status });
            e.HasOne(r => r.Warehouse).WithMany()
             .HasForeignKey(r => r.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Project).WithMany()
             .HasForeignKey(r => r.ProjectCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Material).WithMany()
             .HasForeignKey(r => r.MaterialCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.SourceTransaction).WithMany(t => t.Reservations)
             .HasForeignKey(r => r.SourceTicketCode)
             .HasPrincipalKey(t => t.TicketCode)
             .OnDelete(DeleteBehavior.Cascade);
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
            e.Property(pi => pi.ReservedQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Ignore(pi => pi.ActualInventoryQty);
            e.Ignore(pi => pi.PlannedInventoryQty);
            e.Ignore(pi => pi.ExpectedInventoryQty);
            e.HasOne(pi => pi.Project).WithMany(p => p.Inventories)
             .HasForeignKey(pi => pi.ProjectCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pi => pi.Material).WithMany(m => m.ProjectInventories)
             .HasForeignKey(pi => pi.MaterialCode).OnDelete(DeleteBehavior.Restrict);
            e.Property(pi => pi.RowVersion).IsRowVersion();
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
            e.Property(t => t.WarehouseApprovalStatus)
                .HasConversion(new WarehouseKeeperApprovalStatusConverter())
                .HasMaxLength(20)
                .HasDefaultValue(WarehouseKeeperApprovalStatus.Pending);
            e.Property(t => t.BeneficiaryProjectCode).HasMaxLength(50);
            e.Property(t => t.SupplierName).HasMaxLength(255);
            e.Property(t => t.LinkedTicketCode).HasMaxLength(50);
            e.HasOne(t => t.LinkedTransaction).WithMany()
             .HasForeignKey(t => t.LinkedTicketCode)
             .HasPrincipalKey(wt => wt.TicketCode)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Project).WithMany(p => p.Transactions)
             .HasForeignKey(t => t.ProjectCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(t => t.Supplier).WithMany(s => s.Transactions)
             .HasForeignKey(t => t.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.Property(t => t.RowVersion).IsRowVersion();
        });

        model.Entity<WarehouseTransactionItem>(e =>
        {
            e.ToTable("warehouse_transaction_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialNameSnapshot).HasMaxLength(255);
            e.Property(i => i.UnitSnapshot).HasMaxLength(50);
            e.Property(i => i.Quantity).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(i => i.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();
            e.Property(i => i.ConstructionPhase).HasMaxLength(255);
            e.Property(i => i.SupplierName).HasMaxLength(255);
            e.Ignore(i => i.TotalAmount);
            e.HasOne<Supplier>().WithMany()
             .HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
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
