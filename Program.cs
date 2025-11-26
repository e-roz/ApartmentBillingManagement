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
            builder.Services.AddScoped<ITenantLinkingService, TenantLinkingService>();
            builder.Services.AddScoped<InvoicePdfService>();
            builder.Services.AddScoped<ManagerReportingService>();
            builder.Services.AddScoped<ExcelExportService>();

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
