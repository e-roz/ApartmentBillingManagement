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

        // Tenant Table - Obsolete: Kept for migration compatibility
        [Obsolete("Kept for migration - remove later")]
        public DbSet<Tenant> Tenants { get; set; }

        // Core Application Tables. (Apartment, Bill, Bill Period)
        public DbSet<ApartmentModel> Apartments { get; set; } = null!;
        public DbSet<Bill> Bills { get; set; } = null!;
        public DbSet<BillingPeriod> BillingPeriods { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<PaymentReceipt> PaymentReceipts { get; set; } = null!;

        // Tenant Link Table
        public DbSet<TenantLink> TenantLinks { get; set; }

        // Request Table
        public DbSet<Request> Requests { get; set; }

        // Message Table
        public DbSet<Message> Messages { get; set; }

        // Audit Log Table
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the UserRole enum to be stored as an integer
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<int>();

            // Configure User tenant properties
            modelBuilder.Entity<User>()
                .Property(u => u.LeaseStatus)
                .HasMaxLength(32);

            // Configure User-Apartment relationship
            modelBuilder.Entity<User>()
                .HasOne(u => u.Apartment)
                .WithMany()
                .HasForeignKey(u => u.ApartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure User-Bills relationship
            modelBuilder.Entity<User>()
                .HasMany(u => u.Bills)
                .WithOne(b => b.TenantUser)
                .HasForeignKey(b => b.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure the AuditActionType enum to be stored as a string
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action)
                .HasConversion<string>()
                .HasMaxLength(50);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Set UserId to NULL if the User is deleted

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

            // Obsolete: Tenant-Bill relationship - kept for migration compatibility
            // New relationship is User-Bills via TenantUserId
            modelBuilder.Entity<Tenant>()
                .HasMany(t => t.Bills)
                .WithOne(b => b.Tenant)
                .HasForeignKey(b => b.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<Request>()
                .Property(r => r.RequestType)
                .HasConversion<string>()
                .HasMaxLength(32);
            
            modelBuilder.Entity<Request>()
                .Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(32);
            
            modelBuilder.Entity<Request>()
                .Property(r => r.Priority)
                .HasConversion<string>()
                .HasMaxLength(32);

            // Configure Message relationships to prevent cascade delete issues
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure TenantLink Id as auto-incrementing identity column
            modelBuilder.Entity<TenantLink>()
                .Property(tl => tl.Id)
                .ValueGeneratedOnAdd();

            // Performance indexes for frequently queried columns
            // New index for TenantUserId
            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.TenantUserId)
                .HasDatabaseName("IX_Bills_TenantUserId");

            // Obsolete: Keep old index for migration compatibility
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

            // New index for TenantUserId
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.TenantUserId)
                .HasDatabaseName("IX_Invoices_TenantUserId");

            // Obsolete: Keep old index for migration compatibility
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.TenantId)
                .HasDatabaseName("IX_Invoices_TenantId");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.PaymentDate)
                .HasDatabaseName("IX_Invoices_PaymentDate");

            // Indexes for User tenant properties
            modelBuilder.Entity<User>()
                .HasIndex(u => u.ApartmentId)
                .HasDatabaseName("IX_Users_ApartmentId");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.LeaseStatus)
                .HasDatabaseName("IX_Users_LeaseStatus");

            // Obsolete: Keep Tenant indexes for migration compatibility
            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.ApartmentId)
                .HasDatabaseName("IX_Tenants_ApartmentId");

            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.Status)
                .HasDatabaseName("IX_Tenants_Status");

        }

    }
}
