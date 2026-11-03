using Catalog.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.DAL;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();

    public static CatalogDbContext Create(string connectionString) => new(
        new DbContextOptionsBuilder<CatalogDbContext>().UseSqlServer(connectionString).Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var p = modelBuilder.Entity<Product>();
        p.ToTable("Product", "dbo"); p.HasKey(x => x.ProductId);
        p.Property(x => x.ProductId).HasColumnName("ProductID");
        p.Property(x => x.CategoryId).HasColumnName("CategoryID");
        p.Property(x => x.BrandId).HasColumnName("BrandID");
        p.Property(x => x.CreatedByUserId).HasColumnName("CreatedByUserID");
        p.Property(x => x.ProductCode).HasMaxLength(40).IsUnicode(false);
        p.HasIndex(x => x.ProductCode).IsUnique();
        p.Property(x => x.ProductName).HasMaxLength(200);
        p.Property(x => x.Model).HasMaxLength(100);
        p.Property(x => x.Unit).HasMaxLength(30);
        p.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
        p.Property(x => x.LengthCm).HasPrecision(10, 2);
        p.Property(x => x.WidthCm).HasPrecision(10, 2);
        p.Property(x => x.HeightCm).HasPrecision(10, 2);
        p.Property(x => x.GrossWeightKg).HasPrecision(10, 3);
        p.Property(x => x.UpdatedAt).HasPrecision(3);
        p.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        p.Property(x => x.RowVersion).IsRowVersion();
        p.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        p.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.NoAction);
        var c = modelBuilder.Entity<Category>(); c.ToTable("Category", "dbo"); c.HasKey(x => x.CategoryId);
        c.Property(x => x.CategoryId).HasColumnName("CategoryID"); c.Property(x => x.CategoryName).HasMaxLength(100);
        c.Property(x => x.Description).HasMaxLength(1000);
        var b = modelBuilder.Entity<Brand>(); b.ToTable("Brand", "dbo"); b.HasKey(x => x.BrandId);
        b.Property(x => x.BrandId).HasColumnName("BrandID"); b.Property(x => x.BrandName).HasMaxLength(100);
        b.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
        var s = modelBuilder.Entity<Supplier>(); s.ToTable("Supplier", "dbo"); s.HasKey(x => x.SupplierId);
        s.Property(x => x.SupplierId).HasColumnName("SupplierID"); s.Property(x => x.SupplierName).HasMaxLength(200);
        s.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
        s.Property(x => x.Address).HasMaxLength(500); s.Property(x => x.Phone).HasMaxLength(30); s.Property(x => x.Email).HasMaxLength(254);
        var sp = modelBuilder.Entity<SupplierProduct>(); sp.ToTable("SupplierProduct", "dbo");
        sp.HasKey(x => new { x.SupplierId, x.ProductId });
        sp.Property(x => x.SupplierId).HasColumnName("SupplierID"); sp.Property(x => x.ProductId).HasColumnName("ProductID");
        sp.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.NoAction);
        sp.HasOne(x => x.Product).WithMany(x => x.SupplierProducts).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
    }
}
