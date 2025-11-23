using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageUsersModel : PageModel
    {
        private readonly ApplicationDbContext dbData;

        public List<UserList> Users { get; set; } = new List<UserList>();


        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public ManageUsersModel(ApplicationDbContext dbData)
        {
            this.dbData = dbData;
        }
        //Fetches and filters the list of users 
        public async Task OnGetAsync()
        {
            //get the id of curent logged in admin to exclude from the list
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (currentAdminId == null)
            {
                ErrorMessage = "Authentication error. Cannot determine your user ID.";
                return;
            }

            var query = dbData.Users
             .Where(u => u.Id.ToString() != currentAdminId)
             .AsQueryable();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                // Sanitize and limit search term length to prevent abuse
                var sanitizedTerm = SearchTerm.Trim();
                if (sanitizedTerm.Length > 100)
                {
                    sanitizedTerm = sanitizedTerm.Substring(0, 100);
                }
                
                query = query.Where(u =>
                    u.Username.Contains(sanitizedTerm) ||
                    u.Email.Contains(sanitizedTerm)
                );
            }

            // Execute the query and project the data into the safe UserList 
            Users = query
                .AsEnumerable()
                .Select(u => new UserList
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = (UserRoles)Enum.Parse(typeof(UserRoles), u.Role.ToString()),
                    CreationDate = u.CreatedAt
                })
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Id)
                .ToList();
        }


        // POST Method: Update user role
        public async Task<IActionResult> OnPostUpdateRoleAsync(int userId, string newRole)
        {
            var userToUpdate = await dbData.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (userToUpdate == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            // Security: Prevent admin from changing their own role
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentAdminId != null && int.TryParse(currentAdminId, out int adminId) && userToUpdate.Id == adminId)
            {
                ErrorMessage = "You cannot change your own role. Please ask another administrator to do this.";
                return RedirectToPage();
            }

            // Validate the new role and update 
            if (Enum.TryParse(newRole, true, out UserRoles role))
            {
                userToUpdate.Role = role;
                userToUpdate.UpdatedAt = System.DateTime.UtcNow;

                dbData.Users.Update(userToUpdate);
                await dbData.SaveChangesAsync();

                SuccessMessage = $"{userToUpdate.Username}'s role updated to {role}.";
            }
            else
            {
                ErrorMessage = $"Error: Invalid role value ({newRole}).";
            }
            return RedirectToPage();
        }

        // POST Method: Deletes a user account
        public async Task<IActionResult> OnPostDeleteUserAsync(int userId)
        {
            var userToDelete = await dbData.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (userToDelete == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            // Check security: Prevent the Admin from deleting themselves 
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userToDelete.Id.ToString() == currentAdminId)
            {
                ErrorMessage = "Error: You cannot delete your own account.";
                return RedirectToPage();
            }

            // Check for dependencies before deletion
            if (userToDelete.TenantID.HasValue)
            {
                var tenantId = userToDelete.TenantID.Value;
                
                // Check if tenant has unpaid bills (calculate from invoices, not Bill.AmountPaid)
                var tenantBills = await dbData.Bills
                    .Where(b => b.TenantId == tenantId)
                    .Select(b => b.Id)
                    .ToListAsync();

                if (tenantBills.Any())
                {
                    var billIds = tenantBills.ToList();
                    var invoiceSums = await dbData.Invoices
                        .Where(i => i.BillId.HasValue && billIds.Contains(i.BillId.Value) && i.PaymentDate != null)
                        .GroupBy(i => i.BillId!.Value)
                        .Select(group => new
                        {
                            BillId = group.Key,
                            TotalPaid = group.Sum(i => i.AmountDue)
                        })
                        .ToDictionaryAsync(k => k.BillId, v => v.TotalPaid);

                    var billsWithAmounts = await dbData.Bills
                        .Where(b => b.TenantId == tenantId)
                        .Select(b => new { b.Id, b.AmountDue })
                        .ToListAsync();

                    var hasUnpaidBills = billsWithAmounts.Any(b =>
                    {
                        var paidAmount = invoiceSums.TryGetValue(b.Id, out var paid) ? paid : 0m;
                        return b.AmountDue > paidAmount;
                    });

                    if (hasUnpaidBills)
                    {
                        ErrorMessage = "Cannot delete user. The associated tenant has unpaid bills. Please resolve all outstanding payments first.";
                        return RedirectToPage();
                    }
                }
            }

            // Delete the user
            dbData.Users.Remove(userToDelete);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"User {userToDelete.Username} (ID: {userToDelete.Id}) has been permanently deleted.";
            return RedirectToPage();
        }

    }
}

