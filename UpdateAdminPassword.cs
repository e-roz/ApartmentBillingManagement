
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Apartment.Data;
using Apartment.Enums;
using Apartment.Utilities;

namespace Apartment
{
    public class UpdateAdminPassword
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                })
                .Build();

            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    Console.WriteLine("Attempting to find Admin user...");

                    // Find the admin user
                    var adminUser = await context.Users
                        .FirstOrDefaultAsync(u => u.Role == UserRoles.Admin);

                    if (adminUser != null)
                    {
                        string newPassword = "admin123.";
                        string hashedPassword = PasswordHasher.HashPassword(newPassword);

                        adminUser.HasedPassword = hashedPassword;
                        adminUser.UpdatedAt = DateTime.UtcNow;

                        await context.SaveChangesAsync();
                        Console.WriteLine($"Admin user '{adminUser.Username}' password updated successfully to '{newPassword}'.");
                    }
                    else
                    {
                        Console.WriteLine("Admin user not found. Please ensure an admin user exists.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
    }
}
