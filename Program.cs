using Apartment.Data;
using Apartment.Options;
using Apartment.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
namespace Apartment
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();


            // Configure Entity Framework and SQL Server
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Register application services
            builder.Services.Configure<LogSnagOptions>(builder.Configuration.GetSection("LogSnag"));
            builder.Services.AddHttpClient<ILogSnagClient, LogSnagClient>();
            // TenantLinkingService removed - functionality merged into User model
            builder.Services.AddScoped<InvoicePdfService>();
            builder.Services.AddScoped<AdminReportingService>();
            builder.Services.AddScoped<ExcelExportService>();
            builder.Services.AddScoped<AuditLogPdfService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IAuditService, AuditService>();

            // Add Cookie Authentication Service
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";

                    options.LogoutPath = "/Logout";

                    options.AccessDeniedPath = "/AccessDenied";

                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // cookie expiry time
                    options.SlidingExpiration = true; // renew cookie on each requst
                });

            var app = builder.Build();

            // Automatically apply database migrations on startup to ensure the DB is up to date
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    // Ensure the database is created and all pending migrations are applied.
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                    // In a real production scenario, you might want to handle this more gracefully.
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Add authentication middleware BEFORE authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();

        }
    }
}
