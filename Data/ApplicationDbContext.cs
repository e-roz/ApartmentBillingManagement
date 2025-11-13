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

            // Configure the one to many relationship between apartment and User (Tenant)
            modelBuilder.Entity<ApartmentModel>()
                .HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .IsRequired(false) // tenantId is nullable
                .OnDelete(DeleteBehavior.Restrict); // prevents deleting a user if they are assigned as a tenant to any apartment

        }

    }
}
