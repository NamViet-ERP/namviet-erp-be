using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Converters;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.SharedKernel.Common;
using System.Text.Json;

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
    public DbSet<SupplierGroup> SupplierGroups => Set<SupplierGroup>();
    public DbSet<SupplierGroupMember> SupplierGroupMembers => Set<SupplierGroupMember>();
    public DbSet<SupplierContact> SupplierContacts => Set<SupplierContact>();
    public DbSet<SupplierEvaluation> SupplierEvaluations => Set<SupplierEvaluation>();
    public DbSet<SupplierDeliveryEvaluation> SupplierDeliveryEvaluations => Set<SupplierDeliveryEvaluation>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectWarehouseKeeper> ProjectWarehouseKeepers => Set<ProjectWarehouseKeeper>();
    public DbSet<ProjectConstructionTask> ProjectConstructionTasks => Set<ProjectConstructionTask>();
    public DbSet<ProjectGanttSnapshot> ProjectGanttSnapshots => Set<ProjectGanttSnapshot>();
    public DbSet<ProjectGanttSnapshotItem> ProjectGanttSnapshotItems => Set<ProjectGanttSnapshotItem>();

    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<ProjectBottleneck> ProjectBottlenecks => Set<ProjectBottleneck>();
    public DbSet<ProjectRequest> ProjectRequests => Set<ProjectRequest>();
    public DbSet<ProjectMeeting> ProjectMeetings => Set<ProjectMeeting>();
    public DbSet<ProjectMeetingAction> ProjectMeetingActions => Set<ProjectMeetingAction>();

    public DbSet<ProjectInventory> ProjectInventories => Set<ProjectInventory>();
    public DbSet<ProjectMaterialLoan> ProjectMaterialLoans => Set<ProjectMaterialLoan>();
    public DbSet<WarehouseTransaction> WarehouseTransactions => Set<WarehouseTransaction>();
    public DbSet<WarehouseTransactionItem> WarehouseTransactionItems => Set<WarehouseTransactionItem>();
    public DbSet<WarehouseTransactionAttachment> WarehouseTransactionAttachments => Set<WarehouseTransactionAttachment>();
    public DbSet<WmsWarehouse> WmsWarehouses => Set<WmsWarehouse>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<Stocktake> Stocktakes => Set<Stocktake>();
    public DbSet<StocktakeItem> StocktakeItems => Set<StocktakeItem>();
    public DbSet<ConsumableQuota> ConsumableQuotas => Set<ConsumableQuota>();
    public DbSet<ProjectBom> ProjectBoms => Set<ProjectBom>();
    public DbSet<PurchasePlan> PurchasePlans => Set<PurchasePlan>();
    public DbSet<PurchasePlanLine> PurchasePlanLines => Set<PurchasePlanLine>();
    public DbSet<PurchasePlanLineQuote> PurchasePlanLineQuotes => Set<PurchasePlanLineQuote>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<PurchaseOrderAttachment> PurchaseOrderAttachments => Set<PurchaseOrderAttachment>();
    public DbSet<PurchaseOrderComment> PurchaseOrderComments => Set<PurchaseOrderComment>();
    public DbSet<PurchaseOrderInstallment> PurchaseOrderInstallments => Set<PurchaseOrderInstallment>();
    public DbSet<PurchaseOrderInstallmentDocument> PurchaseOrderInstallmentDocuments => Set<PurchaseOrderInstallmentDocument>();
    public DbSet<PurchaseDefect> PurchaseDefects => Set<PurchaseDefect>();
    public DbSet<PurchaseDefectItem> PurchaseDefectItems => Set<PurchaseDefectItem>();
    public DbSet<PurchaseDefectAttachment> PurchaseDefectAttachments => Set<PurchaseDefectAttachment>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();
    public DbSet<PurchaseWarranty> PurchaseWarranties => Set<PurchaseWarranty>();
    public DbSet<PurchaseWarrantyClaim> PurchaseWarrantyClaims => Set<PurchaseWarrantyClaim>();
    public DbSet<PurchaseContract> PurchaseContracts => Set<PurchaseContract>();
    public DbSet<PurchaseEstimate> PurchaseEstimates => Set<PurchaseEstimate>();
    public DbSet<PurchaseEstimateVersion> PurchaseEstimateVersions => Set<PurchaseEstimateVersion>();
    public DbSet<PurchaseEstimateLine> PurchaseEstimateLines => Set<PurchaseEstimateLine>();
    public DbSet<PurchaseEstimateProposal> PurchaseEstimateProposals => Set<PurchaseEstimateProposal>();
    public DbSet<PurchaseEstimateApproval> PurchaseEstimateApprovals => Set<PurchaseEstimateApproval>();
    public DbSet<PurchaseRfqQuote> PurchaseRfqQuotes => Set<PurchaseRfqQuote>();
    public DbSet<PurchaseSellPrice> PurchaseSellPrices => Set<PurchaseSellPrice>();
    public DbSet<PurchaseItemEstimatePrice> PurchaseItemEstimatePrices => Set<PurchaseItemEstimatePrice>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
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
            e.Property(m => m.CeilingPrice).HasColumnType("numeric(18,2)");
            e.Property(m => m.AbcClass).HasMaxLength(1);
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
            e.Property(s => s.PaymentTerms).HasMaxLength(500);
            e.Property(s => s.CreditLimit).HasColumnType("numeric(18,2)");
            e.Property(s => s.Rating).HasMaxLength(10).HasDefaultValue("D");
            e.Property(s => s.ApprovalStatus).HasMaxLength(20).HasDefaultValue("new");
            e.HasIndex(s => s.ApprovalStatus);
            e.Property(s => s.IsBlacklisted).HasDefaultValue(false);
            e.Property(s => s.BlacklistedByName).HasMaxLength(200);
            e.Property(s => s.BlacklistReason).HasMaxLength(1000);
            e.HasIndex(s => s.IsBlacklisted);
            e.HasQueryFilter(s => s.DeletedAt == null);
        });

        model.Entity<SupplierGroup>(e =>
        {
            e.ToTable("supplier_groups");
            e.HasKey(g => g.Id);
            e.Property(g => g.Name).HasMaxLength(255).IsRequired();
            e.Property(g => g.Description).HasMaxLength(1000);
            // Soft delete: chỉ ràng buộc trùng tên trên bản ghi còn hiệu lực.
            e.HasIndex(g => g.Name).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasQueryFilter(g => g.DeletedAt == null);
        });

        model.Entity<SupplierGroupMember>(e =>
        {
            e.ToTable("supplier_group_members");
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.SupplierGroupId, m.SupplierId }).IsUnique();
            e.HasIndex(m => m.SupplierId);
            e.HasOne(m => m.Group).WithMany(g => g.Members)
             .HasForeignKey(m => m.SupplierGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Supplier).WithMany(s => s.GroupMemberships)
             .HasForeignKey(m => m.SupplierId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<SupplierContact>(e =>
        {
            e.ToTable("supplier_contacts");
            e.HasKey(c => c.Id);
            e.Property(c => c.FullName).HasMaxLength(255).IsRequired();
            e.Property(c => c.Position).HasMaxLength(150);
            e.Property(c => c.Phone).HasMaxLength(50);
            e.Property(c => c.Email).HasMaxLength(255);
            e.Property(c => c.Note).HasMaxLength(1000);
            e.Property(c => c.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(c => c.SupplierId);
            e.HasOne(c => c.Supplier).WithMany(s => s.Contacts)
             .HasForeignKey(c => c.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        model.Entity<SupplierEvaluation>(e =>
        {
            e.ToTable("supplier_evaluations");
            e.HasKey(v => v.Id);
            e.Property(v => v.SupplierNameSnapshot).HasMaxLength(255);
            e.Property(v => v.Period).HasMaxLength(30).IsRequired();
            e.Property(v => v.Score).HasColumnType("numeric(5,2)");
            var criteriaScores = e.Property(v => v.CriteriaScores).HasColumnType("jsonb").IsRequired();
            if (Database.ProviderName is not null
                && !Database.ProviderName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                criteriaScores.HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null)
                         ?? new Dictionary<string, int>(StringComparer.Ordinal));
            }
            e.Property(v => v.Grade).HasMaxLength(10).IsRequired();
            e.Property(v => v.Note).HasMaxLength(2000);
            e.Property(v => v.EvaluatedByName).HasMaxLength(200);
            e.HasIndex(v => new { v.SupplierId, v.Period })
             .IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(v => v.EvaluatedAt);
            e.HasOne(v => v.Supplier).WithMany(s => s.Evaluations)
             .HasForeignKey(v => v.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(v => v.DeletedAt == null);
        });

        model.Entity<SupplierDeliveryEvaluation>(e =>
        {
            e.ToTable("supplier_delivery_evaluations");
            e.HasKey(v => v.Id);
            e.Property(v => v.SupplierNameSnapshot).HasMaxLength(255);
            e.Property(v => v.PoCode).HasMaxLength(50).IsRequired();
            e.Property(v => v.ImportTicketCode).HasMaxLength(50).IsRequired();
            e.Property(v => v.ProjectCode).HasMaxLength(50);
            e.Property(v => v.Status).HasMaxLength(20).IsRequired();
            e.Property(v => v.Quality).HasColumnType("numeric(4,2)");
            e.Property(v => v.Delivery).HasColumnType("numeric(4,2)");
            e.Property(v => v.Price).HasColumnType("numeric(4,2)");
            e.Property(v => v.Service).HasColumnType("numeric(4,2)");
            e.Property(v => v.Compliance).HasColumnType("numeric(4,2)");
            e.Property(v => v.Score).HasColumnType("numeric(5,2)");
            e.Property(v => v.Grade).HasMaxLength(10);
            e.Property(v => v.Note).HasMaxLength(2000);
            e.Property(v => v.EvaluatedByName).HasMaxLength(200);
            e.HasIndex(v => v.ImportTicketCode)
             .IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(v => v.PoCode);
            e.HasIndex(v => new { v.Status, v.DueAt });
            e.HasIndex(v => v.SupplierId);
            e.HasOne(v => v.Supplier).WithMany(s => s.DeliveryEvaluations)
             .HasForeignKey(v => v.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(v => v.DeletedAt == null);
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
            e.Property(p => p.ClientName).HasMaxLength(200);
            e.Property(p => p.ScMembers).HasMaxLength(500);
            e.HasQueryFilter(p => p.DeletedAt == null);
            e.HasMany(p => p.WarehouseKeepers).WithOne(k => k.Project)
             .HasForeignKey(k => k.ProjectCode).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.ConstructionTasks).WithOne(t => t.Project)
             .HasForeignKey(t => t.ProjectCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ProjectConstructionTask>(e =>
        {
            e.ToTable("project_construction_tasks");
            e.HasKey(t => t.Id);
            e.Property(t => t.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(t => t.Name).HasMaxLength(255).IsRequired();
            e.Property(t => t.WbsCode).HasMaxLength(50);
            e.Property(t => t.Department).HasMaxLength(255);
            e.Property(t => t.StatusLabel).HasMaxLength(100);
            e.Property(t => t.StatusTone).HasMaxLength(20);
            e.HasIndex(t => t.ProjectCode);
            e.HasIndex(t => new { t.ProjectCode, t.SortOrder });
            e.HasQueryFilter(t => t.DeletedAt == null);
        });

        model.Entity<ProjectWarehouseKeeper>(e =>
        {
            e.ToTable("project_warehouse_keepers");
            e.HasKey(k => k.Id);
            e.Property(k => k.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(k => k.DisplayName).HasMaxLength(255).IsRequired();
            e.HasIndex(k => new { k.ProjectCode, k.UserId }).IsUnique();
            e.HasIndex(k => k.UserId);
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
            e.Property(pi => pi.MinStockQty).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Ignore(pi => pi.ActualInventoryQty);
            e.Ignore(pi => pi.AvailableQty);
            e.Ignore(pi => pi.PlannedInventoryQty);
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
            e.Property(t => t.PurchasePlanCode).HasMaxLength(50);
            e.Property(t => t.SoCode).HasMaxLength(100);
            e.Property(t => t.Customer).HasMaxLength(255);
            e.Property(t => t.WarehouseKeeper).HasMaxLength(255);
            e.Property(t => t.WarehouseApprovalStatus)
                .HasConversion(new WarehouseKeeperApprovalStatusConverter())
                .HasMaxLength(20)
                .HasDefaultValue(WarehouseKeeperApprovalStatus.Pending);
            e.Property(t => t.BeneficiaryProjectCode).HasMaxLength(50);
            e.Property(t => t.SupplierName).HasMaxLength(255);
            e.Property(t => t.ReceiptEvaluation).HasMaxLength(2000);
            e.Property(t => t.ShortageReport).HasMaxLength(2000);
            e.Property(t => t.LinkedDefectTicketCode).HasMaxLength(50);
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
            e.HasQueryFilter(t => t.DeletedAt == null);
        });

        model.Entity<WarehouseTransactionItem>(e =>
        {
            e.ToTable("warehouse_transaction_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialNameSnapshot).HasMaxLength(255);
            e.Property(i => i.UnitSnapshot).HasMaxLength(50);
            e.Property(i => i.Quantity).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(i => i.PlannedQuantity).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(i => i.AcceptedQty).HasColumnType("numeric(18,4)");
            e.Property(i => i.DefectQty).HasColumnType("numeric(18,4)");
            e.Property(i => i.ShortageQty).HasColumnType("numeric(18,4)");
            e.Property(i => i.DefectLineReason).HasMaxLength(1000);
            e.Property(i => i.ShortageLineReason).HasMaxLength(1000);
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

        model.Entity<WarehouseTransactionAttachment>(e =>
        {
            e.ToTable("warehouse_transaction_attachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.Kind).HasMaxLength(20).IsRequired();
            e.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.HasIndex(a => a.TicketCode);
            e.HasOne(a => a.Transaction).WithMany(t => t.Attachments)
             .HasForeignKey(a => a.TicketCode).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(i => i.RowVersion).IsRowVersion();
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
            e.Property(i => i.Reason).HasMaxLength(500);
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

        model.Entity<PurchasePlan>(e =>
        {
            e.ToTable("purchase_plans");
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(p => p.Status).HasMaxLength(30).IsRequired();
            e.Property(p => p.Note).HasMaxLength(2000);
            e.Property(p => p.CreatedByName).HasMaxLength(200);
            e.Property(p => p.BudgetTotal).HasColumnType("numeric(18,2)");
            e.Property(p => p.ProposalTotal).HasColumnType("numeric(18,2)");
            e.Property(p => p.PurchaseTotal).HasColumnType("numeric(18,2)");
            e.Property(p => p.PaidTotal).HasColumnType("numeric(18,2)");
            e.HasOne(p => p.Project).WithMany()
             .HasForeignKey(p => p.ProjectCode).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.ProjectCode);
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.CreatedAt);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        model.Entity<PurchasePlanLine>(e =>
        {
            e.ToTable("purchase_plan_lines");
            e.HasKey(l => l.Id);
            e.Property(l => l.PlanCode).HasMaxLength(50).IsRequired();
            e.Property(l => l.SystemName).HasMaxLength(200);
            e.Property(l => l.Category).HasMaxLength(200);
            e.Property(l => l.MaterialCode).HasMaxLength(100);
            e.Property(l => l.ProductName).HasMaxLength(500).IsRequired();
            e.Property(l => l.Spec).HasMaxLength(1000);
            e.Property(l => l.Unit).HasMaxLength(50);
            e.Property(l => l.Quantity).HasColumnType("numeric(18,4)");
            e.Property(l => l.KgPerUnit).HasColumnType("numeric(18,4)");
            e.Property(l => l.LineNote).HasMaxLength(2000);
            e.Property(l => l.PurchaseStatus).HasMaxLength(100);
            e.Property(l => l.StatusNote).HasMaxLength(1000);
            e.Property(l => l.BudgetUnitPrice).HasColumnType("numeric(18,2)");
            e.Property(l => l.BudgetAmount).HasColumnType("numeric(18,2)");
            e.Property(l => l.ProposalKgPerUnit).HasColumnType("numeric(18,4)");
            e.Property(l => l.ProposalUnitPrice).HasColumnType("numeric(18,2)");
            e.Property(l => l.ProposalAmount).HasColumnType("numeric(18,2)");
            e.Property(l => l.ProposalSupplierName).HasMaxLength(300);
            e.Property(l => l.ProposalNote).HasMaxLength(2000);
            e.Property(l => l.ProposalStatus).HasMaxLength(100);
            e.Property(l => l.DeliveryNote).HasMaxLength(2000);
            e.Property(l => l.PurchaseKgPerUnit).HasColumnType("numeric(18,4)");
            e.Property(l => l.PurchaseUnitPrice).HasColumnType("numeric(18,2)");
            e.Property(l => l.PurchaseAmount).HasColumnType("numeric(18,2)");
            e.Property(l => l.PurchaseSupplierName).HasMaxLength(300);
            e.Property(l => l.PurchaseNote).HasMaxLength(2000);
            e.Property(l => l.SelectedUnitPrice).HasColumnType("numeric(18,2)");
            e.Property(l => l.SelectedAmount).HasColumnType("numeric(18,2)");
            e.Property(l => l.SelectedSupplierName).HasMaxLength(300);
            e.Property(l => l.SelectionReason).HasMaxLength(1000);
            e.Property(l => l.OrderedQty).HasColumnType("numeric(18,4)");
            e.Property(l => l.ReceivedQty).HasColumnType("numeric(18,4)");
            e.Property(l => l.DefectQty).HasColumnType("numeric(18,4)");
            e.Ignore(l => l.RemainingOrderableQty);
            e.HasOne(l => l.Plan).WithMany(p => p.Lines)
             .HasForeignKey(l => l.PlanCode).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.PlanCode, l.LineNo }).IsUnique();
            e.HasIndex(l => l.MaterialCode);
        });

        model.Entity<PurchasePlanLineQuote>(e =>
        {
            e.ToTable("purchase_plan_line_quotes");
            e.HasKey(q => q.Id);
            e.Property(q => q.KgPerUnit).HasColumnType("numeric(18,4)");
            e.Property(q => q.UnitPrice).HasColumnType("numeric(18,2)");
            e.Property(q => q.Amount).HasColumnType("numeric(18,2)");
            e.Property(q => q.SupplierName).HasMaxLength(300);
            e.Property(q => q.Note).HasMaxLength(2000);
            e.Property(q => q.Status).HasMaxLength(100);
            e.HasOne(q => q.PlanLine).WithMany(l => l.Quotes)
             .HasForeignKey(q => q.PlanLineId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(q => new { q.PlanLineId, q.SortOrder });
        });

        model.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("purchase_orders");
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.ProjectCode).HasMaxLength(50);
            e.Property(p => p.PlanCode).HasMaxLength(50).IsRequired();
            e.Property(p => p.ApprovalStatus).HasMaxLength(30).IsRequired();
            e.Property(p => p.OrderPlacementStatus).HasMaxLength(30).IsRequired();
            e.Property(p => p.DeliveryStatus).HasMaxLength(30).IsRequired();
            e.Property(p => p.PaymentStatus).HasMaxLength(30).IsRequired();
            e.Property(p => p.Note).HasMaxLength(2000);
            e.Property(p => p.TotalAmount).HasColumnType("numeric(18,2)");
            e.Property(p => p.SupplierNameSnapshot).HasMaxLength(300);
            e.Property(p => p.ApprovedByName).HasMaxLength(200);
            e.Property(p => p.PlacedByName).HasMaxLength(200);
            e.Property(p => p.CreatedByName).HasMaxLength(200);
            e.HasOne(p => p.Project).WithMany()
             .HasForeignKey(p => p.ProjectCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(p => p.Plan).WithMany(pl => pl.PurchaseOrders)
             .HasForeignKey(p => p.PlanCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Supplier).WithMany()
             .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasIndex(p => p.PlanCode);
            e.HasIndex(p => p.ProjectCode);
            e.HasIndex(p => p.OrderPlacementStatus);
            e.HasIndex(p => p.DeliveryStatus);
            e.HasIndex(p => p.PaymentStatus);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        model.Entity<PurchaseOrderItem>(e =>
        {
            e.ToTable("purchase_order_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.Property(i => i.Sku).HasMaxLength(100);
            e.Property(i => i.Name).HasMaxLength(500);
            e.Property(i => i.Spec).HasMaxLength(1000);
            e.Property(i => i.Unit).HasMaxLength(50);
            e.Property(i => i.Note).HasMaxLength(2000);
            e.Property(i => i.EstPrice).HasColumnType("numeric(18,2)");
            e.Property(i => i.PoPrice).HasColumnType("numeric(18,2)");
            e.Property(i => i.Ordered).HasColumnType("numeric(18,4)");
            e.Property(i => i.Received).HasColumnType("numeric(18,4)");
            e.Property(i => i.Defected).HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            e.Property(i => i.WarrantyMonths).HasDefaultValue(0);
            e.Property(i => i.WarrantyAnchorType).HasMaxLength(20).HasDefaultValue("delivery");
            e.HasOne(i => i.PurchaseOrder).WithMany(p => p.Items)
             .HasForeignKey(i => i.PoCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.PlanLine).WithMany(l => l.PurchaseOrderItems)
             .HasForeignKey(i => i.PlanLineId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(i => i.Material).WithMany()
             .HasForeignKey(i => i.MaterialCode).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });

        model.Entity<PurchaseWarranty>(e =>
        {
            e.ToTable("purchase_warranties");
            e.HasKey(w => w.Id);
            e.Property(w => w.Code).HasMaxLength(30).IsRequired();
            e.Property(w => w.PoCode).HasMaxLength(50).IsRequired();
            e.Property(w => w.ProjectCode).HasMaxLength(50);
            e.Property(w => w.PlanCode).HasMaxLength(50);
            e.Property(w => w.SupplierName).HasMaxLength(255);
            e.Property(w => w.MaterialCode).HasMaxLength(100);
            e.Property(w => w.MaterialName).HasMaxLength(500).IsRequired();
            e.Property(w => w.Unit).HasMaxLength(50);
            e.Property(w => w.Quantity).HasColumnType("numeric(18,4)");
            e.Property(w => w.AnchorType).HasMaxLength(20).IsRequired();
            e.Property(w => w.Status).HasMaxLength(30).IsRequired();
            e.Property(w => w.ImportTicketCode).HasMaxLength(50);
            e.HasIndex(w => w.Code).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(w => w.PoItemId).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(w => w.PoCode);
            e.HasIndex(w => w.ProjectCode);
            e.HasIndex(w => w.SupplierId);
            e.HasIndex(w => w.MaterialCode);
            e.HasQueryFilter(w => w.DeletedAt == null);
            e.HasOne(w => w.PurchaseOrder).WithMany()
             .HasForeignKey(w => w.PoCode).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.PurchaseOrderItem).WithMany()
             .HasForeignKey(w => w.PoItemId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseWarrantyClaim>(e =>
        {
            e.ToTable("purchase_warranty_claims");
            e.HasKey(c => c.Id);
            e.Property(c => c.TicketCode).HasMaxLength(30).IsRequired();
            e.Property(c => c.Kind).HasMaxLength(20).IsRequired().HasDefaultValue("warranty");
            e.Property(c => c.Status).HasMaxLength(30).IsRequired();
            e.Property(c => c.Reason).HasMaxLength(2000).IsRequired();
            e.Property(c => c.Note).HasMaxLength(2000);
            e.Property(c => c.DefectType).HasMaxLength(20);
            e.Property(c => c.Severity).HasMaxLength(20);
            e.Property(c => c.ServiceNote).HasMaxLength(2000);
            e.Property(c => c.CreatedByName).HasMaxLength(200).IsRequired();
            e.HasIndex(c => c.TicketCode).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(c => c.WarrantyId);
            e.HasIndex(c => c.Kind);
            e.HasQueryFilter(c => c.DeletedAt == null);
            e.HasOne(c => c.Warranty).WithMany(w => w.Claims)
             .HasForeignKey(c => c.WarrantyId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseContract>(e =>
        {
            e.ToTable("purchase_contracts");
            e.HasKey(c => c.Id);
            e.Property(c => c.Code).HasMaxLength(30).IsRequired();
            e.Property(c => c.ContractNo).HasMaxLength(100).IsRequired();
            e.Property(c => c.Title).HasMaxLength(500).IsRequired();
            e.Property(c => c.PoCode).HasMaxLength(50);
            e.Property(c => c.ProjectCode).HasMaxLength(50);
            e.Property(c => c.SupplierName).HasMaxLength(255);
            e.Property(c => c.Status).HasMaxLength(30).IsRequired();
            e.Property(c => c.Amount).HasColumnType("numeric(18,2)");
            e.Property(c => c.Note).HasMaxLength(2000);
            e.Property(c => c.FileName).HasMaxLength(255);
            e.Property(c => c.StorageKey).HasMaxLength(500);
            e.Property(c => c.UploadedByName).HasMaxLength(200);
            e.HasIndex(c => c.Code).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(c => c.ProjectCode);
            e.HasIndex(c => c.SupplierId);
            e.HasIndex(c => c.PoCode);
            e.HasIndex(c => c.Status);
            e.HasIndex(c => new { c.PoCode, c.StorageKey })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"PoCode\" IS NOT NULL AND \"StorageKey\" IS NOT NULL");
            e.HasQueryFilter(c => c.DeletedAt == null);
            e.HasOne(c => c.PurchaseOrder).WithMany()
             .HasForeignKey(c => c.PoCode).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasOne(c => c.Supplier).WithMany()
             .HasForeignKey(c => c.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        model.Entity<PurchaseEstimate>(e =>
        {
            e.ToTable("purchase_estimates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.ProjectCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.PreparerName).HasMaxLength(200);
            e.Property(x => x.ReviewedByName).HasMaxLength(200);
            e.Property(x => x.ConvertedPlanCode).HasMaxLength(30);
            e.Property(x => x.CreatedByName).HasMaxLength(200).IsRequired();
            e.Property(x => x.UpdatedByName).HasMaxLength(200).IsRequired();
            e.Property(x => x.ShareTokenExpiresAt);
            e.HasIndex(x => x.Code).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(x => x.ProjectCode);
            e.HasIndex(x => x.ShareToken).IsUnique().HasFilter("\"ShareToken\" IS NOT NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        model.Entity<PurchaseEstimateVersion>(e =>
        {
            e.ToTable("purchase_estimate_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.UpdatedByName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.EstimateId, x.VersionNumber }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Estimate).WithMany(p => p.Versions)
             .HasForeignKey(x => x.EstimateId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseEstimateLine>(e =>
        {
            e.ToTable("purchase_estimate_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.MaterialCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(x => x.CategoryName).HasMaxLength(200);
            e.Property(x => x.Unit).HasMaxLength(50);
            e.Property(x => x.OldQty).HasColumnType("numeric(18,4)");
            e.Property(x => x.NewQty).HasColumnType("numeric(18,4)");
            e.Property(x => x.EstimateUnitPrice).HasColumnType("numeric(18,2)");
            e.HasIndex(x => new { x.VersionId, x.MaterialCode });
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Version).WithMany(v => v.Lines)
             .HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseEstimateProposal>(e =>
        {
            e.ToTable("purchase_estimate_proposals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Spec).HasMaxLength(500);
            e.Property(x => x.Unit).HasMaxLength(50);
            e.Property(x => x.Qty).HasColumnType("numeric(18,4)");
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Estimate).WithMany(p => p.Proposals)
             .HasForeignKey(x => x.EstimateId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseEstimateApproval>(e =>
        {
            e.ToTable("purchase_estimate_approvals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.ActionByName).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.EstimateId, x.Level }).IsUnique();
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.HasOne(x => x.Estimate).WithMany(p => p.Approvals)
             .HasForeignKey(x => x.EstimateId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseRfqQuote>(e =>
        {
            e.ToTable("purchase_rfq_quotes");
            e.HasKey(x => x.Id);
            e.Property(x => x.MaterialCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(x => x.SupplierName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ProjectCode).HasMaxLength(50);
            e.Property(x => x.UnitPrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.PaymentTerm).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.PoRef).HasMaxLength(50);
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => x.QuoteDate);
            e.HasIndex(x => x.SupplierId);
            e.HasIndex(x => x.MaterialCode);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        model.Entity<PurchaseSellPrice>(e =>
        {
            e.ToTable("purchase_sell_prices");
            e.HasKey(x => x.Id);
            e.Property(x => x.MaterialCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ProjectCode).HasMaxLength(50);
            e.Property(x => x.SellPrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.CreatedByName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => new { x.MaterialCode, x.ProjectCode, x.EffectiveDate });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        model.Entity<PurchaseItemEstimatePrice>(e =>
        {
            e.ToTable("purchase_item_estimate_prices");
            e.HasKey(x => x.Id);
            e.Property(x => x.MaterialCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.MaterialName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ProjectCode).HasMaxLength(50);
            e.Property(x => x.EstimatePrice).HasColumnType("numeric(18,2)");
            e.Property(x => x.CreatedByName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => new { x.MaterialCode, x.ProjectCode, x.EffectiveDate });
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        model.Entity<PurchaseOrderAttachment>(e =>
        {
            e.ToTable("purchase_order_attachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.PoCode).HasMaxLength(50).IsRequired();
            e.Property(a => a.DocType).HasMaxLength(20).IsRequired();
            e.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.UploadedByName).HasMaxLength(200);
            e.HasIndex(a => a.PoCode);
            e.HasIndex(a => new { a.PoCode, a.DocType });
            e.HasOne(a => a.PurchaseOrder).WithMany(p => p.Attachments)
             .HasForeignKey(a => a.PoCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseOrderComment>(e =>
        {
            e.ToTable("purchase_order_comments");
            e.HasKey(c => c.Id);
            e.Property(c => c.PoCode).HasMaxLength(50).IsRequired();
            e.Property(c => c.AuthorName).HasMaxLength(200).IsRequired();
            e.Property(c => c.Body).HasMaxLength(4000).IsRequired();
            e.HasIndex(c => c.PoCode);
            e.HasIndex(c => new { c.PoCode, c.CreatedAt });
            e.HasQueryFilter(c => c.DeletedAt == null);
            e.HasOne(c => c.PurchaseOrder).WithMany(p => p.Comments)
             .HasForeignKey(c => c.PoCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseOrderInstallment>(e =>
        {
            e.ToTable("purchase_order_installments");
            e.HasKey(i => i.Id);
            e.Property(i => i.PoCode).HasMaxLength(50).IsRequired();
            e.Property(i => i.Percent).HasColumnType("numeric(5,2)").IsRequired();
            e.Property(i => i.Status).HasMaxLength(20).IsRequired();
            e.Property(i => i.RequiredDocTypes).HasColumnType("jsonb").IsRequired();
            e.HasIndex(i => new { i.PoCode, i.StageNumber }).IsUnique();
            e.HasOne(i => i.PurchaseOrder).WithMany(p => p.Installments)
             .HasForeignKey(i => i.PoCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseOrderInstallmentDocument>(e =>
        {
            e.ToTable("purchase_order_installment_documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.DocType).HasMaxLength(50).IsRequired();
            e.Property(d => d.FileName).HasMaxLength(255).IsRequired();
            e.Property(d => d.ContentType).HasMaxLength(200).IsRequired();
            e.Property(d => d.StoragePath).HasMaxLength(500).IsRequired();
            e.Property(d => d.UploadedByName).HasMaxLength(200);
            e.HasIndex(d => d.InstallmentId);
            e.HasIndex(d => new { d.InstallmentId, d.DocType });
            e.HasOne(d => d.Installment).WithMany(i => i.Documents)
             .HasForeignKey(d => d.InstallmentId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseDefect>(e =>
        {
            e.ToTable("purchase_defects");
            e.HasKey(d => d.TicketCode);
            e.HasIndex(d => d.Id).IsUnique();
            e.Property(d => d.TicketCode).HasMaxLength(50);
            e.Property(d => d.SourceType).HasMaxLength(20).IsRequired();
            e.Property(d => d.PoCode).HasMaxLength(50);
            e.Property(d => d.ImportTicketCode).HasMaxLength(50);
            e.Property(d => d.PlanCode).HasMaxLength(50);
            e.Property(d => d.ProjectCode).HasMaxLength(50);
            e.Property(d => d.SupplierName).HasMaxLength(255);
            e.Property(d => d.Status).HasMaxLength(20).IsRequired();
            e.Property(d => d.Reason).HasMaxLength(2000);
            e.Property(d => d.Note).HasMaxLength(2000);
            e.Property(d => d.CauseSource).HasMaxLength(30);
            e.Property(d => d.SupplierFeedback).HasMaxLength(4000);
            e.Property(d => d.HandlingMethod).HasMaxLength(40);
            e.Property(d => d.CreatedByName).HasMaxLength(255);
            e.Property(d => d.ApprovedByName).HasMaxLength(255);
            e.Property(d => d.ProcessedByName).HasMaxLength(255);
            e.HasIndex(d => d.PoCode);
            e.HasIndex(d => d.ImportTicketCode);
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.SourceType);
            e.HasOne(d => d.PurchaseOrder).WithMany()
             .HasForeignKey(d => d.PoCode)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(d => d.DeletedAt == null);
        });

        model.Entity<PurchaseDefectItem>(e =>
        {
            e.ToTable("purchase_defect_items");
            e.HasKey(i => i.Id);
            e.Property(i => i.TicketCode).HasMaxLength(50).IsRequired();
            e.Property(i => i.MaterialCode).HasMaxLength(100);
            e.Property(i => i.Name).HasMaxLength(500).IsRequired();
            e.Property(i => i.Unit).HasMaxLength(50);
            e.Property(i => i.Quantity).HasColumnType("numeric(18,4)").IsRequired();
            e.Property(i => i.LineReason).HasMaxLength(1000);
            e.HasIndex(i => i.PoItemId);
            e.HasOne(i => i.Defect).WithMany(d => d.Items)
             .HasForeignKey(i => i.TicketCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchaseDefectAttachment>(e =>
        {
            e.ToTable("purchase_defect_attachments");
            e.HasKey(a => a.Id);
            e.Property(a => a.TicketCode).HasMaxLength(50).IsRequired();
            e.Property(a => a.Kind).HasMaxLength(20).IsRequired();
            e.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.HasIndex(a => a.TicketCode);
            e.HasOne(a => a.Defect).WithMany(d => d.Attachments)
             .HasForeignKey(a => a.TicketCode).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<PurchasePayment>(e =>
        {
            e.ToTable("purchase_payments");
            e.HasKey(p => p.Code);
            e.Property(p => p.Code).HasMaxLength(50);
            e.Property(p => p.PoCode).HasMaxLength(50).IsRequired();
            e.Property(p => p.StageNumber).IsRequired();
            e.Property(p => p.SupplierNameSnapshot).HasMaxLength(300);
            e.Property(p => p.InvoiceNo).HasMaxLength(100);
            e.Property(p => p.Amount).HasColumnType("numeric(18,2)");
            e.Property(p => p.Status).HasMaxLength(30).IsRequired();
            e.Property(p => p.Note).HasMaxLength(2000);
            e.Property(p => p.CreatedByName).HasMaxLength(200);
            e.Property(p => p.PaidByName).HasMaxLength(200);
            e.HasOne(p => p.PurchaseOrder).WithMany()
             .HasForeignKey(p => p.PoCode).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Supplier).WithMany()
             .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasIndex(p => p.PoCode);
            // Một đợt chỉ có 1 DNTT đang hiệu lực (không tính cancelled / soft-delete).
            e.HasIndex(p => new { p.PoCode, p.StageNumber })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND lower(\"Status\") <> 'cancelled'");
            e.HasIndex(p => p.Status);
            e.HasIndex(p => p.DueDate);
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        model.Entity<ActivityEvent>(e =>
        {
            e.ToTable("activity_events");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActorName).HasMaxLength(200).IsRequired();
            e.Property(a => a.Module).HasMaxLength(50).IsRequired();
            e.Property(a => a.Action).HasMaxLength(80).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(50);
            e.Property(a => a.EntityCode).HasMaxLength(80);
            e.Property(a => a.Message).HasMaxLength(2000).IsRequired();
            e.Property(a => a.CategoryLabel).HasMaxLength(100).IsRequired();
            e.HasIndex(a => new { a.Module, a.OccurredAt });
            e.HasIndex(a => new { a.ActorUserId, a.OccurredAt });
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
