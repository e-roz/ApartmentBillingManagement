using System;
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

namespace Apartment.Pages.Manager
{
    [Authorize(Roles = "Manager")]
    public class RequestDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RequestDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; } // Request ID from URL

        public Request TenantRequest { get; set; } // Renamed from Request
        public IList<Message> Messages { get; set; }

        [BindProperty]
        public string ReplyContent { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }
        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (Id == 0)
            {
                return NotFound();
            }

            TenantRequest = await _context.Requests // Updated reference
                .Include(r => r.SubmittedByUser)
                .Include(r => r.Apartment)
                .FirstOrDefaultAsync(m => m.Id == Id);

            if (TenantRequest == null) // Updated reference
            {
                return NotFound();
            }

            Messages = await _context.Messages
                .Where(m => m.AssociatedRequestId == Id)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // Mark messages from tenant as read by manager
            var managerUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            foreach (var message in Messages.Where(m => m.ReceiverUserId == managerUserId && !m.IsRead))
            {
                message.IsRead = true;
            }
            await _context.SaveChangesAsync();


            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Id == 0)
            {
                ErrorMessage = "Request ID is missing.";
                return RedirectToPage("./ViewAllRequests");
            }

            TenantRequest = await _context.Requests // Updated reference
                .Include(r => r.SubmittedByUser)
                .FirstOrDefaultAsync(m => m.Id == Id);

            if (TenantRequest == null) // Updated reference
            {
                ErrorMessage = "Request not found.";
                return RedirectToPage("./ViewAllRequests");
            }

            if (string.IsNullOrWhiteSpace(ReplyContent))
            {
                ErrorMessage = "Reply content cannot be empty.";
                // Re-load the page data
                await OnGetAsync();
                return Page();
            }

            var managerUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Create new message
            var newMessage = new Message
            {
                Content = ReplyContent,
                SenderUserId = managerUserId,
                ReceiverUserId = TenantRequest.SubmittedByUserId.Value, // Updated reference
                AssociatedRequestId = TenantRequest.Id, // Updated reference
                Timestamp = DateTime.UtcNow,
                IsRead = false // Tenant hasn't read it yet
            };

            _context.Messages.Add(newMessage);

            // Update request status if it's currently Submitted
            if (TenantRequest.Status == Enums.RequestStatus.Submitted) // Updated reference
            {
                TenantRequest.Status = Enums.RequestStatus.InProgress; // Updated reference
            }
            // Optionally, you could add another status like "Replied"
            // else if (TenantRequest.Status == Enums.RequestStatus.InProgress)
            // {
            //    TenantRequest.Status = Enums.RequestStatus.Replied;
            // }


            try
            {
                await _context.SaveChangesAsync();
                SuccessMessage = "Reply sent successfully and request status updated!";
                ReplyContent = string.Empty; // Clear reply box
                return RedirectToPage(new { Id = Id }); // Redirect to clear form submission
            }
            catch (Exception ex)
            {
                // Log the exception
                ErrorMessage = "Error sending reply: " + ex.Message;
                await OnGetAsync(); // Re-load page data on error
                return Page();
            }
        }
    }
}