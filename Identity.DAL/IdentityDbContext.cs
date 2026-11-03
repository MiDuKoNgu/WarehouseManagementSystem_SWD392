using Identity.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Identity.DAL;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public static string ConfigureConnection(string connectionString) => new SqlConnectionStringBuilder(connectionString)
        { ConnectTimeout = 5, ConnectRetryCount = 0 }.ConnectionString;
    public static IdentityDbContext Create(string connectionString) => new(
        new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(ConfigureConnection(connectionString), sql => sql.CommandTimeout(5)).Options);
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var u = modelBuilder.Entity<User>(); u.ToTable("User", "dbo"); u.HasKey(x => x.UserId);
        u.Property(x => x.UserId).HasColumnName("UserID"); u.Property(x => x.RoleId).HasColumnName("RoleID");
        u.Property(x => x.WarehouseId).HasColumnName("WarehouseID");
        u.Property(x => x.Username).HasMaxLength(100); u.HasIndex(x => x.Username).IsUnique();
        u.Property(x => x.PasswordHash).HasMaxLength(512); u.Property(x => x.FullName).HasMaxLength(150);
        u.Property(x => x.Email).HasMaxLength(254); u.Property(x => x.Phone).HasMaxLength(30);
        u.Property(x => x.Status).HasMaxLength(32).IsUnicode(false);
        u.Property(x => x.CreatedAt).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        u.Property(x => x.RowVersion).IsRowVersion();
        u.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
        var r = modelBuilder.Entity<Role>(); r.ToTable("Role", "dbo"); r.HasKey(x => x.RoleId);
        r.Property(x => x.RoleId).HasColumnName("RoleID"); r.Property(x => x.RoleName).HasMaxLength(32).IsUnicode(false);
    }
}
