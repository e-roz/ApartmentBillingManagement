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

        // Core Application Tables. (Apartment, Bill, Bill Period)
        public DbSet<ApartmentModel> Apartments { get; set; } = null!;
        public DbSet<Bill> Bills { get; set; } = null!;
        public DbSet<BillingPeriod> BillingPeriods { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<PaymentReceipt> PaymentReceipts { get; set; } = null!;
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; } = null!;

        // Request Table
        public DbSet<Request> Requests { get; set; }

        // Message Table
        public DbSet<Message> Messages { get; set; }

        // Audit Log Table
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Lease Table
        public DbSet<Lease> Leases { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the UserRole enum to be stored as an integer
            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<int>();


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

            // Configure User-Bill relationship
            modelBuilder.Entity<User>()
                .HasMany<Bill>(u => u.Bills)
                .WithOne(b => b.TenantUser)
                .HasForeignKey(b => b.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ApartmentModel-Lease relationship (one-to-many)
            modelBuilder.Entity<ApartmentModel>()
                .HasMany(a => a.Leases)
                .WithOne(l => l.Apartment)
                .HasForeignKey(l => l.ApartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure User-Lease relationship (one-to-many)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Leases)
                .WithOne(l => l.User)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Configure the BillStatus enum to be stored as an integer
            modelBuilder.Entity<Bill>()
                .Property(b => b.Status)
                .HasConversion<int>();

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

            // Performance indexes for frequently queried columns
            modelBuilder.Entity<Bill>()
                .HasIndex(b => b.TenantUserId)
                .HasDatabaseName("IX_Bills_TenantUserId");

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
                .HasIndex(i => i.TenantUserId)
                .HasDatabaseName("IX_Invoices_TenantUserId");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.DateFullySettled)
                .HasDatabaseName("IX_Invoices_PaymentDate");

            modelBuilder.Entity<PaymentAllocation>()
                .HasOne(pa => pa.Invoice)
                .WithMany(i => i.PaymentAllocations)
                .HasForeignKey(pa => pa.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting invoice if allocations exist

            modelBuilder.Entity<PaymentAllocation>()
                .HasOne(pa => pa.Bill)
                .WithMany(b => b.PaymentAllocations)
                .HasForeignKey(pa => pa.BillId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting bill if allocations exist

            modelBuilder.Entity<PaymentAllocation>()
                .HasIndex(pa => pa.InvoiceId);

            modelBuilder.Entity<PaymentAllocation>()
                .HasIndex(pa => pa.BillId);



            // Performance indexes for Lease
            modelBuilder.Entity<Lease>()
                .HasIndex(l => l.UserId)
                .HasDatabaseName("IX_Leases_UserId");

            modelBuilder.Entity<Lease>()
                .HasIndex(l => l.ApartmentId)
                .HasDatabaseName("IX_Leases_ApartmentId");

            modelBuilder.Entity<Lease>()
                .HasIndex(l => l.LeaseStart)
                .HasDatabaseName("IX_Leases_LeaseStart");

            modelBuilder.Entity<Lease>()
                .HasIndex(l => l.LeaseEnd)
                .HasDatabaseName("IX_Leases_LeaseEnd");

        }

    }
}
