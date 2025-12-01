using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Apartment.Pages.Tenant
{
    [Authorize(Roles = "Tenant")]
    public class MessagesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MessagesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Model.User? UserInfo { get; set; }

        public IList<Model.Message> Messages { get; set; } = new List<Model.Message>();

        [TempData]
        public string? SuccessMessage { get; set; }
        [TempData]
        public string? ErrorMessage { get; set; }
        public int CurrentUserId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Forbid(); // User not identified or not logged in
            }
            CurrentUserId = userId; // Set CurrentUserId

            var user = await _context.Users
                .Include(u => u.Leases)
                    .ThenInclude(l => l.Apartment)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user != null)
            {
                UserInfo = user;
            } else {
                // If a user is logged in but account not found, it's an inconsistent state.
                ErrorMessage = "Your user account was not found. Please contact support.";
                return RedirectToPage("/TenantDashboard"); // Redirect to a safe page
            }


            Messages = await _context.Messages
                .Where(m => m.ReceiverUserId == userId || m.SenderUserId == userId) // Get messages sent to or by the tenant
                .Include(m => m.Sender)
                .Include(m => m.AssociatedRequest)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            // Mark unread messages as read when tenant views them
            foreach (var message in Messages.Where(m => m.ReceiverUserId == userId && !m.IsRead))
            {
                message.IsRead = true;
            }
            await _context.SaveChangesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int messageId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                ErrorMessage = "User not identified. Please log in again.";
                return RedirectToPage("/Tenant/Messages"); // Explicit redirect
            }

            var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
            {
                ErrorMessage = "Message not found.";
                return RedirectToPage("/Tenant/Messages"); // Explicit redirect
            }

            // Ensure tenant can only delete messages they received or sent
            if (message.ReceiverUserId != userId && message.SenderUserId != userId)
            {
                ErrorMessage = "You are not authorized to delete this message.";
                return RedirectToPage("/Tenant/Messages"); // Explicit redirect
            }

            try
            {
                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();
                SuccessMessage = "Message deleted successfully!";
            }
            catch (Exception ex)
            {
                // Log the exception
                ErrorMessage = $"Error deleting message: {ex.Message}";
            }

            return RedirectToPage("/Tenant/Messages"); // Explicit redirect
        }
    }
}