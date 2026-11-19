using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Data
{
    public class MiniMartDbContext : DbContext
    {
        public MiniMartDbContext(DbContextOptions<MiniMartDbContext> options) : base(options)
        {
        }

        public DbSet<Store> Stores { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<StoreProduct> StoreProducts { get; set; } = null!;
        public DbSet<ProductBatch> ProductBatches { get; set; } = null!;
        public DbSet<ProductItem> ProductItems { get; set; } = null!;
        public DbSet<PriceHistory> PriceHistories { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceItem> InvoiceItems { get; set; } = null!;
        public DbSet<InvoiceItemBatch> InvoiceItemBatches { get; set; } = null!;
        public DbSet<ImportReceipt> ImportReceipts { get; set; } = null!;
        public DbSet<ImportReceiptItem> ImportReceiptItems { get; set; } = null!;
        public DbSet<StockTransfer> StockTransfers { get; set; } = null!;
        public DbSet<StockTransferItem> StockTransferItems { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<PromotionItem> PromotionItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraints
            modelBuilder.Entity<Store>()
                .HasIndex(s => s.StoreCode).IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<Supplier>()
                .HasIndex(s => s.SupplierCode).IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.ProductCode).IsUnique();
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode).IsUnique();
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Name);

            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.MemberCode).IsUnique();
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Phone).IsUnique();

            modelBuilder.Entity<StoreProduct>()
                .HasIndex(sp => new { sp.StoreId, sp.ProductId }).IsUnique();

            modelBuilder.Entity<ProductBatch>()
                .HasIndex(pb => pb.BatchBarcode).IsUnique();

            modelBuilder.Entity<ProductItem>()
                .HasIndex(pi => pi.UnitBarcode).IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceCode).IsUnique();
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => new { i.StoreId, i.CreatedAt });

            modelBuilder.Entity<ImportReceipt>()
                .HasIndex(ir => ir.ReceiptCode).IsUnique();
            modelBuilder.Entity<ImportReceipt>()
                .HasIndex(ir => new { ir.StoreId, ir.ImportDate });

            modelBuilder.Entity<InventoryTransaction>()
                .HasIndex(it => new { it.StoreId, it.ProductId });

            // Relationships & Delete Behaviors
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Cashier)
                .WithMany()
                .HasForeignKey(i => i.CashierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Store)
                .WithMany(s => s.Invoices)
                .HasForeignKey(i => i.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ImportReceipt>()
                .HasOne(ir => ir.CreatedByUser)
                .WithMany()
                .HasForeignKey(ir => ir.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ImportReceipt>()
                .HasOne(ir => ir.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(ir => ir.ConfirmedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ImportReceipt>()
                .HasOne(ir => ir.CancelledByUser)
                .WithMany()
                .HasForeignKey(ir => ir.CancelledBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransfer>()
                .HasOne(st => st.FromStore)
                .WithMany()
                .HasForeignKey(st => st.FromStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransfer>()
                .HasOne(st => st.ToStore)
                .WithMany()
                .HasForeignKey(st => st.ToStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StoreProduct>()
                .HasOne(sp => sp.Store)
                .WithMany(s => s.StoreProducts)
                .HasForeignKey(sp => sp.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StoreProduct>()
                .HasOne(sp => sp.Product)
                .WithMany(p => p.StoreProducts)
                .HasForeignKey(sp => sp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

