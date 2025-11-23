using Apartment.Model;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Data
{
    public class ApplicationDbContext : DbContext
    {
        //Constractor to accept to accept database context options
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // User Table
        public DbSet<User> Users { get; set; }

        // Tenant Table
        public DbSet<Tenant> Tenants { get; set; }

        // Core Application Tables. (Apartment, Bill, Bill Period)
        public DbSet<ApartmentModel> Apartments { get; set; } = null!;
        public DbSet<Bill> Bills { get; set; } = null!;
        public DbSet<BillingPeriod> BillingPeriods { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<PaymentReceipt> PaymentReceipts { get; set; } = null!;

        // Tenant Link Table
        public DbSet<TenantLink> TenantLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the UserRole enum to be stored as an integer
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<int>();

            //Ensure the periodKey is unique in BillingPeriod
            modelBuilder.Entity<BillingPeriod>()
                .HasIndex(bp => bp.PeriodKey)
                .IsUnique();

            // Configure the one to many relationship between ApartmentModel and Bill
            modelBuilder.Entity<ApartmentModel>()
                .HasMany(a => a.Bills)
                .WithOne(b => b.Apartment)
                .HasForeignKey(b => b.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade); // prevents deleting an apartment if it has associated bills

            // Configure the one to many relationship between Tenant and Bill
            modelBuilder.Entity<Tenant>()
                .HasMany(t => t.Bills)
                .WithOne(b => b.Tenant)
                .HasForeignKey(b => b.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // prevents deleting a tenant if they have associated bills

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Configure TenantLink Id as auto-incrementing identity column
            modelBuilder.Entity<TenantLink>()
                .Property(tl => tl.Id)
                .ValueGeneratedOnAdd();

            // Performance indexes for frequently queried columns
            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.TenantId)
                .HasDatabaseName("IX_Bills_TenantId");

            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.BillingPeriodId)
                .HasDatabaseName("IX_Bills_BillingPeriodId");

            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.DueDate)
                .HasDatabaseName("IX_Bills_DueDate");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.BillId)
                .HasDatabaseName("IX_Invoices_BillId");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.TenantId)
                .HasDatabaseName("IX_Invoices_TenantId");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.PaymentDate)
                .HasDatabaseName("IX_Invoices_PaymentDate");

            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.ApartmentId)
                .HasDatabaseName("IX_Tenants_ApartmentId");

            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.Status)
                .HasDatabaseName("IX_Tenants_Status");

        }

    }
}
