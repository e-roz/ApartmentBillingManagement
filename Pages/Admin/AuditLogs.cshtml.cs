using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Apartment.Pages.Admin
{

    [Authorize(Roles = "Admin")]
    public class AuditLogsModel : PageModel
    {
      
    }
}

