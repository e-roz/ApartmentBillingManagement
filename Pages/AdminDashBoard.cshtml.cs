using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Apartment.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminDashBoardModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
