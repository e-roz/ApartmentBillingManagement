using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Apartment.Pages
{
    [Authorize(Roles = "Manager,User")]
    public class DashboardModel : PageModel
    {
        public string Username { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        public void OnGet()
        {
            // Get user information from claims
            Username = User.Identity?.Name ?? "Unknown User";
            UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown Role";
        }
    }
}
