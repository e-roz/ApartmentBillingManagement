using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Apartment.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages
{
    public class LoginModel : PageModel
    {

        private readonly ApplicationDbContext dbData; // Holds the connection object to database


        [BindProperty]
        public Login Input { get; set; } = new Login();

        [TempData]
        public string? Message { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public LoginModel(ApplicationDbContext context)
        {
            dbData = context;
        }
        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            //find the user in the database by the provided email
            var user = await dbData.Users
                .FirstOrDefaultAsync(u => u.Email == Input.Email);


            //c n check yung user existence at v n veriufy yung password gamit secure hasher
            if (user == null || !PasswordHasher.VerifyPassword(Input.Password, user.HasedPassword))
            {
                ErrorMessage = "Invalid email or password.";
                return Page();
            }

            var claims = new List<Claim>
            {
                //store unique user id 
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

                //store the email as the name (for display purposes)
                new Claim(ClaimTypes.Name, user.Email),

                //add the Role claim
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            //create identity obj based on the claims and cookie 
            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,  //keeps the cookie after browser close
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddMinutes(30) //cookie expiration

            };

            //sign in the user in (creates and sned the secure cookie to the browser)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
                );




            // Redirect users to their role-specific dashboards

            if (user.Role == UserRoles.Admin)
            {
                return RedirectToPage("/Admin/AdminDashboard");
            }
            else if (user.Role == UserRoles.Manager)
            {
                return RedirectToPage("/Manager/ManagerDashboard");
            }
            else
            {
                return RedirectToPage("/TenantDashboard");
            }
        }
    }
}
