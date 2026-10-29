using Microsoft.EntityFrameworkCore;

namespace IMSB.Dto.Models;

public class IMSBContext : DbContext
{
    public IMSBContext(DbContextOptions<IMSBContext> options) : base(options) { }

    public DbSet<Staff> Staff { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<Login> Logins { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }  
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<SalesEntry> SalesEntries { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<Artisan> Artisans { get; set; }
    public DbSet<Stall> Stalls { get; set; }
    public DbSet<StallReallocation> StallReallocations { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<ArtisanApplication> ArtisanApplications { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<LeaveEntitlement> LeaveEntitlements { get; set; }
    public DbSet<StaffLeaveEntry> StaffLeaveEntries { get; set; }
    public DbSet<PayBillEntry> PayBillEntries { get; set; }

    // Fixed ids so the seeded categories are stable across environments
    public static readonly (Guid Id, string Name)[] SeedCategories =
    {
        (Guid.Parse("a1000000-0000-0000-0000-000000000001"), "Textiles"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000002"), "Handicrafts"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000003"), "Jewellery"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000004"), "Pottery"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000005"), "Paintings"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000006"), "Herbal"),
        (Guid.Parse("a1000000-0000-0000-0000-000000000007"), "Food & Snacks"),
    };

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Staff>(b =>
        {
            b.HasIndex(e => e.EmployeeId).IsUnique();
            b.HasIndex(e => e.Mobile).IsUnique();
            b.HasIndex(e => e.DepartmentId);

            b.Property(p => p.EmployeeId).HasMaxLength(20);
            b.Property(p => p.Mobile).HasMaxLength(32);
            b.Property(p => p.Email).HasMaxLength(200);
            b.Property(p => p.StaffCategory).HasMaxLength(100);
        });

        modelBuilder.Entity<Department>(b =>
        {
            b.HasIndex(d => d.Name).IsUnique(false);
        });

        modelBuilder.Entity<Shift>(b =>
        {
            b.HasIndex(s => s.Code).IsUnique();
        });

        modelBuilder.Entity<Login>(b =>
        {
            b.HasKey(l => l.Id);
            b.HasIndex(l => l.Username).IsUnique();
            b.Property(l => l.Username).HasMaxLength(100).IsRequired();
            b.Property(l => l.Email).HasMaxLength(200);
            b.Property(l => l.PasswordHash).IsRequired();
            b.Property(l => l.IsActive).HasDefaultValue(true);
            b.Property(l => l.Role).HasMaxLength(50).IsRequired().HasDefaultValue("Admin");
            b.ToTable("logins");
        });

        modelBuilder.Entity<PasswordResetToken>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.Token).IsUnique();
            b.Property(p => p.Token).IsRequired();
            b.Property(p => p.ExpiresAt).IsRequired();
            b.HasOne(p => p.Login).WithMany().HasForeignKey(p => p.LoginId).OnDelete(DeleteBehavior.Cascade);
            b.ToTable("password_reset_tokens");
        });

        modelBuilder.Entity<ProductCategory>(b =>
        {
            b.HasIndex(c => c.Name).IsUnique();
            b.HasData(SeedCategories.Select((c, i) => new ProductCategory { Id = c.Id, Name = c.Name, SortOrder = i + 1 }));
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.HasIndex(p => p.ProductCode).IsUnique();
            b.HasIndex(p => p.CategoryId);
            b.HasIndex(p => p.StallCode);
            // a category in use cannot be deleted
            b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.HasIndex(m => m.ProductId);
            b.HasIndex(m => m.CreatedAt);
            // deleting a product keeps its history (product_code/product_name are snapshots)
            b.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SalesEntry>(b =>
        {
            b.HasIndex(e => e.EntryNo).IsUnique();
            b.HasIndex(e => e.SoldAt);
            b.HasMany(e => e.Items).WithOne(i => i.SalesEntry).HasForeignKey(i => i.SalesEntryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleItem>(b =>
        {
            b.HasIndex(i => i.ProductId);
            b.HasIndex(i => i.StallCode);
            // deleting a product keeps its sales (code/name/category are snapshots)
            b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Artisan>(b =>
        {
            b.HasIndex(a => a.ArtisanCode).IsUnique();
            b.HasIndex(a => a.Mobile).IsUnique();
            b.HasIndex(a => a.AadhaarHash).IsUnique();
            b.HasIndex(a => a.Status);
            b.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Stall>(b =>
        {
            b.HasIndex(s => s.StallCode).IsUnique();
            b.HasIndex(s => s.Status);
            // an artisan holds at most one stall
            b.HasIndex(s => s.ArtisanId).IsUnique().HasFilter("[artisan_id] IS NOT NULL");
            b.HasOne(s => s.Category).WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
            // an allocated artisan cannot be deleted (the controller asks to vacate first)
            b.HasOne(s => s.Artisan).WithMany().HasForeignKey(s => s.ArtisanId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StallReallocation>(b =>
        {
            b.HasIndex(r => r.RequestNo).IsUnique();
            b.HasIndex(r => new { r.StallId, r.Status });
            b.HasOne(r => r.Stall).WithMany().HasForeignKey(r => r.StallId).OnDelete(DeleteBehavior.Cascade);
            // artisan links are informational (names are snapshotted); SQL Server allows only one cascade path,
            // so the controller clears them when an artisan is deleted
            b.HasOne<Artisan>().WithMany().HasForeignKey(r => r.CurrentArtisanId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Artisan>().WithMany().HasForeignKey(r => r.NewArtisanId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSession>(b =>
        {
            b.HasIndex(s => s.LoginId);
            b.HasIndex(s => s.ExpiresAt);
            b.HasOne(s => s.Login).WithMany().HasForeignKey(s => s.LoginId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArtisanApplication>(b =>
        {
            b.HasIndex(a => a.ApplicationNo).IsUnique();
            b.HasIndex(a => a.Status);
            b.HasIndex(a => a.SubmittedDate);
            // not unique: a rejected applicant may apply again (the controller blocks duplicates among open ones)
            b.HasIndex(a => a.Mobile);
            b.HasIndex(a => a.AadhaarHash);
            b.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.Artisan).WithMany().HasForeignKey(a => a.ArtisanId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AttendanceRecord>(b =>
        {
            // one record per staff member per day; saving the sheet again updates it
            b.HasIndex(a => new { a.StaffId, a.AttendanceDate }).IsUnique();
            b.HasIndex(a => a.AttendanceDate);
            b.HasIndex(a => a.Status);
            // deleting a staff member removes their attendance (leavers are normally set INACTIVE instead)
            b.HasOne(a => a.Staff).WithMany().HasForeignKey(a => a.StaffId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveEntitlement>(b =>
        {
            b.HasIndex(e => new { e.StaffId, e.LeaveType, e.Year }).IsUnique()
                .HasFilter("[staff_id] IS NOT NULL");
            b.HasIndex(e => new { e.StaffId, e.Year }).IsUnique()
                .HasFilter("[attendance_leave_type] = 1");
            b.HasOne(e => e.Staff).WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.Cascade);
            b.Property(e => e.StaffCategory).HasMaxLength(100).IsRequired();
            b.Property(e => e.LeaveType).HasMaxLength(100).IsRequired();
            b.Property(e => e.AnnualQuota).HasColumnType("decimal(8,2)");
        });

        modelBuilder.Entity<StaffLeaveEntry>(b =>
        {
            b.HasIndex(e => new { e.StaffId, e.FromDate });
            b.HasIndex(e => e.Year);
            b.Property(e => e.LeaveType).HasMaxLength(100).IsRequired();
            b.Property(e => e.Days).HasColumnType("decimal(6,2)");
            b.Property(e => e.Remarks).HasMaxLength(500);
            b.HasOne(e => e.Staff).WithMany().HasForeignKey(e => e.StaffId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayBillEntry>(b =>
        {
            b.HasIndex(e => new { e.StaffId, e.Year, e.Month }).IsUnique();
            b.HasIndex(e => new { e.Year, e.Month });
        });
    }
}
