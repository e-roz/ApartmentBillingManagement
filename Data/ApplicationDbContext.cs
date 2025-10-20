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

        public DbSet<User> Users { get; set; }

    }
}
