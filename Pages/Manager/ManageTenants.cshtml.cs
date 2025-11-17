using Apartment.Data;
using Apartment.Model;
using Apartment.ViewModels;
using Apartment.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;

namespace Apartment.Pages.Manager
{
    [Authorize(Roles = "Manager")]
    public class ManageTenantsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManageTenantsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Properties for Index (List View)
        public IList<TenantListViewModel> Tenants { get; private set; } = new List<TenantListViewModel>();

        // Properties for Details
        public Model.Tenant? TenantDetails { get; private set; }

        // Properties for Create/Edit
        [BindProperty]
        public Model.Tenant Tenant { get; set; } = new Model.Tenant();

        [BindProperty]
        public int SelectedApartmentId { get; set; }

        public List<SelectListItem> ApartmentOptions { get; private set; } = new List<SelectListItem>();
        
        public Dictionary<int, decimal> ApartmentRents { get; private set; } = new Dictionary<int, decimal>();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // ========== INDEX (List View) ==========
        public async Task OnGetAsync()
        {
            Tenants = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.Apartment)
                .OrderBy(t => t.FullName)
                .Select(t => new TenantListViewModel
                {
                    Id = t.Id,
                    FullName = t.FullName,
                    PrimaryEmail = t.PrimaryEmail,
                    PrimaryPhone = t.PrimaryPhone,
                    MonthlyRent = t.MonthlyRent,
                    LeaseStatus = t.Status.ToString(),
                    AssignedUnit = t.Apartment != null ? t.Apartment.UnitNumber : "Unassigned"
                })
                .ToListAsync();
        }

        // ========== DETAILS (Single View) ==========
        public async Task<IActionResult> OnGetDetailsAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            TenantDetails = await _context.Tenants
                .Include(t => t.Apartment)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id.Value);

            if (TenantDetails == null)
            {
                return NotFound();
            }

            return Page();
        }

        // ========== CREATE (Add New Tenant) ==========
        public async Task<IActionResult> OnGetCreateAsync()
        {
            SelectedApartmentId = 0;
            await PopulateApartmentOptionsForCreateAsync(null);
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            Tenant.ApartmentId = SelectedApartmentId > 0 ? SelectedApartmentId : null;

            // First, validate and set UnitNumber/MonthlyRent from apartment BEFORE ModelState validation
            if (!Tenant.ApartmentId.HasValue || Tenant.ApartmentId.Value == 0)
            {
                ModelState.AddModelError(nameof(SelectedApartmentId), "Please select an apartment unit.");
                await PopulateApartmentOptionsForCreateAsync(null);
                return Page();
            }

            // Get the apartment and set UnitNumber/MonthlyRent BEFORE ModelState validation
            var assignedApartment = await _context.Apartments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == Tenant.ApartmentId.Value);

            if (assignedApartment == null)
            {
                ModelState.AddModelError(nameof(SelectedApartmentId), "The selected apartment no longer exists.");
                await PopulateApartmentOptionsForCreateAsync(Tenant.ApartmentId);
                return Page();
            }

            if (assignedApartment.IsOccupied)
            {
                ModelState.AddModelError(nameof(SelectedApartmentId), "The selected apartment is already occupied.");
                await PopulateApartmentOptionsForCreateAsync(Tenant.ApartmentId);
                return Page();
            }

            // Set UnitNumber and MonthlyRent from the apartment
            Tenant.UnitNumber = assignedApartment.UnitNumber;
            Tenant.MonthlyRent = assignedApartment.MonthlyRent;

            // Remove navigation properties and programmatically-set fields from ModelState validation
            // This prevents validation errors for fields we set ourselves
            ModelState.Remove(nameof(Tenant.Apartment));
            ModelState.Remove(nameof(Tenant.Bills));
            ModelState.Remove(nameof(Tenant.CreatedAt));
            
            // Explicitly remove and clear any errors for UnitNumber and MonthlyRent
            var unitNumberKey = nameof(Tenant.UnitNumber);
            var monthlyRentKey = nameof(Tenant.MonthlyRent);
            
            if (ModelState.ContainsKey(unitNumberKey))
            {
                ModelState[unitNumberKey]!.Errors.Clear();
                ModelState.Remove(unitNumberKey);
            }
            
            if (ModelState.ContainsKey(monthlyRentKey))
            {
                ModelState[monthlyRentKey]!.Errors.Clear();
                ModelState.Remove(monthlyRentKey);
            }

            if (!ModelState.IsValid)
            {
                await PopulateApartmentOptionsForCreateAsync(Tenant.ApartmentId);
                return Page();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Reload apartment within transaction to update IsOccupied
                var apartmentToUpdate = await _context.Apartments
                    .FirstOrDefaultAsync(a => a.Id == Tenant.ApartmentId.Value);

                if (apartmentToUpdate != null)
                {
                    apartmentToUpdate.IsOccupied = true;
                }

                _context.Tenants.Add(Tenant);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                SuccessMessage = "Tenant created successfully.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorMessage = $"An error occurred while creating the tenant: {ex.Message}";
                await PopulateApartmentOptionsForCreateAsync(Tenant.ApartmentId);
                return Page();
            }
        }

        // ========== EDIT (Update Tenant) ==========
        public async Task<IActionResult> OnGetEditAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Tenant = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.Apartment)
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (Tenant == null)
            {
                return NotFound();
            }

            SelectedApartmentId = Tenant.ApartmentId ?? 0;
            await PopulateApartmentOptionsForEditAsync(Tenant.ApartmentId);
            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            Tenant.ApartmentId = SelectedApartmentId > 0 ? SelectedApartmentId : null;

            // Remove navigation properties and fields that will be set programmatically
            ModelState.Remove(nameof(Tenant.Apartment));
            ModelState.Remove(nameof(Tenant.Bills));
            ModelState.Remove(nameof(Tenant.UnitNumber)); // UnitNumber will be set from apartment
            ModelState.Remove(nameof(Tenant.MonthlyRent)); // MonthlyRent will be set from apartment
            ModelState.Remove(nameof(Tenant.CreatedAt)); // CreatedAt should not be changed

            if (!ModelState.IsValid)
            {
                await PopulateApartmentOptionsForEditAsync(Tenant.ApartmentId);
                return Page();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tenantToUpdate = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Id == Tenant.Id);

                if (tenantToUpdate == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                var oldApartmentId = tenantToUpdate.ApartmentId;
                var newApartmentId = Tenant.ApartmentId;

                tenantToUpdate.FullName = Tenant.FullName;
                tenantToUpdate.PrimaryEmail = Tenant.PrimaryEmail;
                tenantToUpdate.PrimaryPhone = Tenant.PrimaryPhone;
                tenantToUpdate.LeaseStartDate = Tenant.LeaseStartDate;
                tenantToUpdate.LeaseEndDate = Tenant.LeaseEndDate;
                tenantToUpdate.Status = Tenant.Status;
                tenantToUpdate.ApartmentId = newApartmentId;

                if (oldApartmentId != newApartmentId)
                {
                    if (oldApartmentId.HasValue)
                    {
                        var previousApartment = await _context.Apartments
                            .FirstOrDefaultAsync(a => a.Id == oldApartmentId.Value);

                        if (previousApartment != null)
                        {
                            previousApartment.IsOccupied = false;
                        }
                    }

                    if (newApartmentId.HasValue)
                    {
                        var newApartment = await _context.Apartments
                            .FirstOrDefaultAsync(a => a.Id == newApartmentId.Value);

                        if (newApartment == null)
                        {
                            ModelState.AddModelError(nameof(SelectedApartmentId), "The selected apartment no longer exists.");
                            await transaction.RollbackAsync();
                            await PopulateApartmentOptionsForEditAsync(oldApartmentId);
                            return Page();
                        }

                        if (newApartment.IsOccupied && newApartment.Id != oldApartmentId)
                        {
                            ModelState.AddModelError(nameof(SelectedApartmentId), "The selected apartment is already occupied.");
                            await transaction.RollbackAsync();
                            await PopulateApartmentOptionsForEditAsync(oldApartmentId);
                            return Page();
                        }

                        // Set UnitNumber and MonthlyRent from the new apartment
                        tenantToUpdate.UnitNumber = newApartment.UnitNumber;
                        tenantToUpdate.MonthlyRent = newApartment.MonthlyRent;
                        newApartment.IsOccupied = true;
                    }
                    else if (!newApartmentId.HasValue)
                    {
                        // If moving out (no apartment selected), keep existing UnitNumber and MonthlyRent
                        // Or you could clear them - adjust based on your business logic
                    }
                }
                else if (newApartmentId.HasValue)
                {
                    // Same apartment, but update MonthlyRent in case apartment rent changed
                    var currentApartment = await _context.Apartments
                        .FirstOrDefaultAsync(a => a.Id == newApartmentId.Value);
                    if (currentApartment != null)
                    {
                        tenantToUpdate.MonthlyRent = currentApartment.MonthlyRent;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                SuccessMessage = "Tenant updated successfully.";
                return RedirectToPage();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                if (!await TenantExistsAsync(Tenant.Id))
                {
                    return NotFound();
                }

                ModelState.AddModelError(string.Empty, "The tenant was updated by another process. Please reload the page and try again.");
                await PopulateApartmentOptionsForEditAsync(Tenant.ApartmentId);
                return Page();
            }
            catch
            {
                await transaction.RollbackAsync();
                ErrorMessage = "An error occurred while updating the tenant. Please try again.";
                await PopulateApartmentOptionsForEditAsync(Tenant.ApartmentId);
                return Page();
            }
        }

        // ========== DELETE (Remove Tenant) ==========
        public async Task<IActionResult> OnGetDeleteAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            TenantDetails = await _context.Tenants
                .AsNoTracking()
                .Include(t => t.Apartment)
                .FirstOrDefaultAsync(m => m.Id == id.Value);

            if (TenantDetails == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // STEP 1: FINANCIAL INTEGRITY CHECK (MANDATORY)
            var hasUnpaidBills = await _context.Bills
                .AsNoTracking()
                .AnyAsync(b => b.TenantId == id.Value && b.AmountPaid < b.AmountDue);

            // STEP 2: BLOCK DELETION if unpaid bills exist
            if (hasUnpaidBills)
            {
                TempData["ErrorMessage"] = "Cannot delete tenant. Please ensure all bills are marked as paid.";
                return RedirectToPage();
            }

            // STEP 3: EXECUTE DELETION with transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Id == id.Value);

                if (tenant == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound();
                }

                var apartmentId = tenant.ApartmentId;

                _context.Tenants.Remove(tenant);

                if (apartmentId.HasValue)
                {
                    var apartment = await _context.Apartments
                        .FirstOrDefaultAsync(a => a.Id == apartmentId.Value);

                    if (apartment != null)
                    {
                        apartment.IsOccupied = false;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                SuccessMessage = "Tenant deleted successfully.";
                return RedirectToPage();
            }
            catch
            {
                await transaction.RollbackAsync();
                ErrorMessage = "An error occurred while deleting the tenant.";
                return RedirectToPage();
            }
        }

        // ========== HELPER METHODS ==========
        private async Task PopulateApartmentOptionsForCreateAsync(int? selectedApartmentId)
        {
            var availableApartments = await _context.Apartments
                .AsNoTracking()
                .Where(a => !a.IsOccupied || (selectedApartmentId.HasValue && a.Id == selectedApartmentId.Value))
                .Select(a => new { a.Id, a.UnitNumber, a.MonthlyRent })
                .ToListAsync();

            ApartmentOptions = new List<SelectListItem>
            {
                new SelectListItem("Unassigned", "0", !selectedApartmentId.HasValue)
            };

            ApartmentRents.Clear();
            foreach (var apartment in availableApartments
                         .GroupBy(a => a.Id)
                         .Select(g => g.First())
                         .OrderBy(a => a.UnitNumber))
            {
                ApartmentOptions.Add(new SelectListItem
                {
                    Text = apartment.UnitNumber,
                    Value = apartment.Id.ToString(),
                    Selected = selectedApartmentId.HasValue && apartment.Id == selectedApartmentId.Value
                });
                ApartmentRents[apartment.Id] = apartment.MonthlyRent;
            }
        }

        private async Task PopulateApartmentOptionsForEditAsync(int? currentApartmentId)
        {
            var apartments = await _context.Apartments
                .AsNoTracking()
                .Where(a => !a.IsOccupied || (currentApartmentId.HasValue && a.Id == currentApartmentId.Value))
                .Select(a => new { a.Id, a.UnitNumber, a.MonthlyRent })
                .ToListAsync();

            ApartmentOptions = new List<SelectListItem>
            {
                new SelectListItem("Move Out", "0", !currentApartmentId.HasValue)
            };

            ApartmentRents.Clear();
            foreach (var apartment in apartments
                         .GroupBy(a => a.Id)
                         .Select(g => g.First())
                         .OrderBy(a => a.UnitNumber))
            {
                ApartmentOptions.Add(new SelectListItem
                {
                    Text = apartment.UnitNumber,
                    Value = apartment.Id.ToString(),
                    Selected = currentApartmentId.HasValue && apartment.Id == currentApartmentId.Value
                });
                ApartmentRents[apartment.Id] = apartment.MonthlyRent;
            }
        }

        private async Task<bool> TenantExistsAsync(int id)
        {
            return await _context.Tenants.AnyAsync(e => e.Id == id);
        }
    }
}

