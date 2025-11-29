using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Apartment.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Apartment.Services;

namespace Apartment.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext dbData; // Holds the connection object to database
        private readonly IAuditService _auditService;

        //receives the data submitted from html form
        [BindProperty]
        public RegisterUser Input { get; set; } = new RegisterUser();


        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? Message { get; set; }

        public RegisterModel(ApplicationDbContext context, IAuditService auditService)
        {
            dbData = context;
            _auditService = auditService;
        }

         
        public async Task<IActionResult> OnPostAsync()
        {
            // Public registration disabled - only Admin can create users via Admin area
            return RedirectToPage("/AccessDenied");
        }


        public IActionResult OnGet()
        {
            // Public registration disabled - only Admin can create users
            return RedirectToPage("/AccessDenied");
        }



    }
}
