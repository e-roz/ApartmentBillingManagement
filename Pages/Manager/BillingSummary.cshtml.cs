using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Apartment.Pages.Manager
{
    [Authorize(Roles = "Manager")]
    public class BillingSummaryModel : PageModel
    {
        
    }
}

