using Apartment.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Apartment.Data;
using Apartment.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Apartment.Pages.Manager
{
    [Authorize(Roles = "Admin")]
    public class RequestDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public RequestDetailsModel(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; } // Request ID from URL

        public Request TenantRequest { get; set; }
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

            TenantRequest = await _context.Requests
                .Include(r => r.SubmittedByUser)
                .Include(r => r.Apartment)
                .FirstOrDefaultAsync(m => m.Id == Id);

            if (TenantRequest == null)
            {
                return NotFound();
            }

            // Messages are no longer displayed.
            Messages = new List<Message>();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Id == 0)
            {
                ErrorMessage = "Request ID is missing.";
                return RedirectToPage("./ViewAllRequests");
            }

            var requestToDelete = await _context.Requests
                .Include(r => r.SubmittedByUser) // We need the user to create the message
                .FirstOrDefaultAsync(r => r.Id == Id);

            if (requestToDelete == null)
            {
                ErrorMessage = "The request you are trying to reply to no longer exists.";
                return RedirectToPage("./ViewAllRequests");
            }

            if (string.IsNullOrWhiteSpace(ReplyContent))
            {
                ErrorMessage = "Reply content cannot be empty. Please write a reply to close the request.";
                TenantRequest = requestToDelete;
                Messages = new List<Message>();
                return Page();
            }

            var managerUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // 1. Create the new message for the tenant, but do not associate it with the request
            var newMessage = new Message
            {
                Content = ReplyContent,
                SenderUserId = managerUserId,
                ReceiverUserId = requestToDelete.SubmittedByUserId.Value,
                AssociatedRequestId = null, // Decouple from the request
                Timestamp = DateTime.UtcNow
            };
            _context.Messages.Add(newMessage);

            // 2. Find and remove any old messages that were associated with the request
            var messagesToDelete = await _context.Messages
                .Where(m => m.AssociatedRequestId == Id)
                .ToListAsync();

            if (messagesToDelete.Any())
            {
                _context.Messages.RemoveRange(messagesToDelete);
            }

            // 3. Remove the request itself
            _context.Requests.Remove(requestToDelete);

            try
            {
                // 4. Save all changes (new message, deleted old messages, deleted request) in one transaction
                await _context.SaveChangesAsync();
                SuccessMessage = $"Reply sent to tenant and request '{requestToDelete.Title}' has been closed.";

                // 5. Log the audit event
                await _auditService.LogAsync(
                    Enums.AuditActionType.CloseRequest,
                    managerUserId,
                    $"Manager replied to and closed request '{requestToDelete.Title}' from user {requestToDelete.SubmittedByUser.Username}.",
                    requestToDelete.Id,
                    nameof(Request)
                );
                await _context.SaveChangesAsync(); // Save the audit log
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "An error occurred while processing the request. Please try again.";
            }

            return RedirectToPage("./ViewAllRequests");
        }
    }
}