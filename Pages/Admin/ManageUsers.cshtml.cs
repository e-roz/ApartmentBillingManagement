using Apartment.Utilities;
using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Apartment.Services;

namespace Apartment.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageUsersModel : PageModel
    {
        private readonly ApplicationDbContext dbData;
        private readonly IAuditService _auditService;

                public List<UserList> Users { get; set; } = new List<UserList>();

        

                [BindProperty(SupportsGet = true)]

                public string? SearchTerm { get; set; }

        

                [BindProperty]

                public RegisterUser NewUser { get; set; } = new RegisterUser();

        [BindProperty]
        public string SelectedRole { get; set; } = UserRoles.User.ToString();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public ManageUsersModel(ApplicationDbContext dbData, IAuditService auditService)
        {
            this.dbData = dbData;
            _auditService = auditService;
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
            var usersFromDb = await query.ToListAsync();

            Users = usersFromDb
                .Select(u => new UserList
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    Role = u.Role,
                    CreationDate = u.CreatedAt
                })
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Id)
                .ToList();
        }

        public async Task<IActionResult> OnPostAddUserAsync()
        {
            if (!ModelState.IsValid)
            {
                ErrorMessage = "Validation failed. Please check your input.";
                await OnGetAsync(); // Re-populate the list of users
                return Page();
            }

            // Check if username or email already exists
            if (await dbData.Users.AnyAsync(u => u.Username == NewUser.Username))
            {
                ModelState.AddModelError("NewUser.Username", "Username already taken.");
                ErrorMessage = "Validation failed: Username already taken.";
                await OnGetAsync();
                return Page();
            }
            if (await dbData.Users.AnyAsync(u => u.Email == NewUser.Email))
            {
                ModelState.AddModelError("NewUser.Email", "Email already registered.");
                ErrorMessage = "Validation failed: Email already registered.";
                await OnGetAsync();
                return Page();
            }

            // Hash password
            var hashedPassword = PasswordHasher.HashPassword(NewUser.Password);

            // Determine the role
            if (!Enum.TryParse(SelectedRole, true, out UserRoles roleToAssign))
            {
                roleToAssign = UserRoles.User; // Default to User if parsing fails
                ErrorMessage = "Invalid role selected. Defaulting to User.";
            }

            // Create new user
            var newUser = new User
            {
                Username = NewUser.Username,
                Email = NewUser.Email,
                HasedPassword = hashedPassword, // Corrected property name
                Role = roleToAssign,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbData.Users.Add(newUser);
            await dbData.SaveChangesAsync();

            // Log audit action
            var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int adminId = -1; 
            if (string.IsNullOrEmpty(adminIdString) || !int.TryParse(adminIdString, out adminId))
            {
                adminId = -1; // Or handle this error more gracefully
            }
            await _auditService.LogAsync(
                action: AuditActionType.CreateUser,
                userId: adminId,
                details: $"Admin added new user: {newUser.Username} (ID: {newUser.Id}, Role: {newUser.Role})",
                entityId: newUser.Id,
                entityType: "User"
            );

            SuccessMessage = $"User {newUser.Username} created successfully with role {newUser.Role}.";
            return RedirectToPage();
        }


        // POST Method: Update user role
        public async Task<IActionResult> OnPostUpdateRoleAsync(int userId, string newRole)
        {
            var userToUpdate = await dbData.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentAdminIdInt = -1;
            if (string.IsNullOrEmpty(adminIdString) || !int.TryParse(adminIdString, out currentAdminIdInt))
            {
                ErrorMessage = "Could not identify the administrator performing the action.";
                return RedirectToPage();
            }

            if (userToUpdate == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            // Security: Prevent admin from changing their own role
            if (userToUpdate.Id == currentAdminIdInt)
            {
                ErrorMessage = "You cannot change your own role. Please ask another administrator to do this.";
                return RedirectToPage();
            }

            // Validate the new role and update 
            if (Enum.TryParse(newRole, true, out UserRoles role))
            {
                // Log the old role before changing
                var oldRole = userToUpdate.Role;

                userToUpdate.Role = role;
                userToUpdate.UpdatedAt = System.DateTime.UtcNow;

                dbData.Users.Update(userToUpdate);
                await dbData.SaveChangesAsync();

                // Log audit action
                await _auditService.LogAsync(
                    action: AuditActionType.UpdateUserRole,
                    userId: currentAdminIdInt,
                    details: $"Admin changed role for user {userToUpdate.Username} (ID: {userToUpdate.Id}) from {oldRole} to {userToUpdate.Role}.",
                    entityId: userToUpdate.Id,
                    entityType: "User"
                );

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
            var currentAdminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userToDelete.Id.ToString() == currentAdminIdString)
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
            var adminIdStringForDelete = User.FindFirstValue(ClaimTypes.NameIdentifier); // Renamed for clarity
            int adminIdForDelete = -1;
            if (string.IsNullOrEmpty(adminIdStringForDelete) || !int.TryParse(adminIdStringForDelete, out adminIdForDelete))
            {
                ErrorMessage = "Could not identify the administrator performing the action.";
                // Decide if you want to stop the deletion or log it as a system action
                // For now, we'll stop it.
                return RedirectToPage();
            }
            // Log the deletion action
            await _auditService.LogAsync(
                action: AuditActionType.DeleteUser,
                userId: adminIdForDelete,
                details: $"User {userToDelete.Username} (ID: {userToDelete.Id}) was deleted by admin (ID: {adminIdForDelete}).",
                entityId: userToDelete.Id,
                entityType: "User"
            );
            // Delete the user
            dbData.Users.Remove(userToDelete);
            await dbData.SaveChangesAsync();

            SuccessMessage = $"User {userToDelete.Username} (ID: {userToDelete.Id}) has been permanently deleted.";
            return RedirectToPage();
        }

    }
}
