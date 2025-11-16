using Apartment.Data;
using Apartment.Model;
using Apartment.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Apartment.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext dbData; // Holds the connection object to database

        //receives the data submitted from html form
        [BindProperty]
        public RegisterUser Input { get; set; } = new RegisterUser();


        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? Message { get; set; }

        public RegisterModel(ApplicationDbContext context)
        {
            dbData = context;
        }

         
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Collect validation errors and redirect to Login with register form shown
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage))
                    .ToList();
                
                if (errors.Any())
                {
                    ErrorMessage = string.Join(" ", errors);
                }
                
                return RedirectToPage("/Login", new { show = "register" });
            }

            //Check if the username already exist
            if(await dbData.Users.AnyAsync(u => u.Username == Input.Username))
            {
                ErrorMessage = "This username is already taken";
                return RedirectToPage("/Login", new { show = "register" });
            }

            //check if this is the very first user in users table
            bool isFirstUser = !await dbData.Users.AnyAsync();

            UserRoles assignedRole = isFirstUser ? UserRoles.Admin : UserRoles.User;


            //create user obj
            var newUser = new User
            {
                Username = Input.Username,
                Email = Input.Email,
                // Hash muna password NO NO NO NO, before i store sa database. Tangina lagi nakakalimutan
                HasedPassword = PasswordHasher.HashPassword(Input.Password),
                Role = assignedRole,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = null

            };

            dbData.Users.Add(newUser);
            await dbData.SaveChangesAsync(); // -> saved in the database


            TempData["Message"] = $"Registration successful! You can now log in. Your role is: {assignedRole}.";
            return RedirectToPage("/Login", new { show = "register" });



        }


        public IActionResult OnGet()
        {
            // Redirect to Login page with register form shown
            return RedirectToPage("/Login", new { show = "register" });
        }



    }
}
