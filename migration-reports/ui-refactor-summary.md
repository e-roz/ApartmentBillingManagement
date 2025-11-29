# UI Refactoring Summary - Tenant to User Consolidation

## Overview
This document summarizes the UI refactoring performed to complete the Tenant-to-User consolidation after database and model refactoring.

**Refactoring Date**: $(Get-Date)
**Branch**: `feature/merge-tenants-ui`
**Status**: ⚠️ **IN PROGRESS**

---

## Step A: Disable Public Registration & Remove Manager Area ✅

### Changes Made

#### 1. Disabled Public Registration
**File**: `Pages/Register.cshtml.cs`

**Before:**
- Public users could register via `/Register` page
- First user became Admin, subsequent users became User role

**After:**
```csharp
public IActionResult OnGet()
{
    // Public registration disabled - only Admin can create users
    return RedirectToPage("/AccessDenied");
}

public async Task<IActionResult> OnPostAsync()
{
    // Public registration disabled - only Admin can create users via Admin area
    return RedirectToPage("/AccessDenied");
}
```

**Impact**: Public registration is now completely disabled. Only Admins can create users.

#### 2. Removed Manager Role
**File**: `Enums/UserRoles.cs`

**Before:**
```csharp
public enum UserRoles
{
    Admin = 1,
    Manager = 2,
    User = 3
}
```

**After:**
```csharp
public enum UserRoles
{
    Admin = 1,
    // Manager role removed - functionality merged into Admin
    User = 3
}
```

#### 3. Updated Authorization Attributes
All Manager pages updated to use Admin role:

**Files Updated:**
- `Pages/Manager/BillingSummary.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/GenerateBills.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/ManageTenants.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/RequestDetails.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/ManagerDashboard.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/ViewAllRequests.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/RecordPayments.cshtml.cs`: `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/ManageApartments.cshtml.cs`: `[Authorize(Roles = "Admin,Manager")]` → `[Authorize(Roles = "Admin")]`
- `Pages/Manager/ManageApartments.cshtml.cs`: `[Authorize(Roles = "Admin,Manager")]` → `[Authorize(Roles = "Admin")]`

#### 4. Updated Login Redirect
**File**: `Pages/Login.cshtml.cs`

**Before:**
```csharp
else if (user.Role == UserRoles.Manager)
{
    return RedirectToPage("/Manager/ManagerDashboard");
}
```

**After:**
```csharp
// Manager role removed - all Manager users should be migrated to Admin
// else if (user.Role == UserRoles.Manager) - Obsolete
```

**Impact**: Manager users will now be redirected to User dashboard. **Note**: Existing Manager users in database should be migrated to Admin role.

#### 5. Updated UI References
**Files Updated:**
- `Pages/Shared/_ManagerSidebar.cshtml`: Default role changed from "Manager" to "Admin"
- `Pages/Admin/ManageUsers.cshtml`: Removed Manager role badge, promotion/demotion buttons, and count

---

## Step B: Admin-only Create/Invite User Flow ⏳

### Status: **PARTIALLY COMPLETE**

### Required Implementation

#### 1. Create User Page
**Path**: `/Pages/Admin/Users/Create.cshtml` + `Create.cshtml.cs`

**Required Fields:**
- Email (required, unique)
- Username (display name)
- Password (temporary, must be changed on first login)
- ApartmentId (dropdown, optional)
- LeaseStart (date, optional)
- LeaseEnd (date, optional)
- LeaseStatus (select: Prospective, Active, Inactive, Evicted)
- Role (default: User, Admin can set to Admin if needed)

**Functionality:**
- Create User record
- Set `MustChangePassword = true` flag (if implemented)
- Log action to AuditLogs
- Optionally send email invite (if email service configured)

#### 2. Invite Token System (Optional Enhancement)
**Model**: `InviteToken.cs`
- Id, UserId, TokenHash, ExpiresAt, IsUsed

**Accept Invite Page**: `/Pages/Account/AcceptInvite.cshtml`
- Validate token
- Allow user to set password
- Mark token as used
- Activate user account

**Current Status**: Not yet implemented. Basic create user flow should be created first.

---

## Step C: Update Razor Pages Using Tenant Model ⏳

### Status: **PENDING**

### Pages Requiring Updates

#### High Priority
1. **TenantDashboard.cshtml.cs**
   - Replace `TenantInfo` with User properties
   - Use `User.ApartmentId`, `User.LeaseStatus` directly
   - Update bill queries to use `TenantUserId`

2. **MakePayment.cshtml.cs**
   - Replace `TenantId` with `TenantUserId`
   - Update queries to use `Bill.TenantUserId`

3. **ViewInvoices.cshtml.cs**
   - Replace `TenantInfo` with User properties
   - Update invoice queries

4. **ManageTenants.cshtml.cs** (Manager area)
   - **Major refactor**: Convert to manage Users with tenant properties
   - Replace all `_context.Tenants` queries with `_context.Users`
   - Filter by `LeaseStatus` or `Role == UserRoles.User`

5. **GenerateBills.cshtml.cs**
   - Replace `_context.Tenants` with `_context.Users`
   - Query users with `LeaseStatus == "Active"`
   - Use `User.Id` for `Bill.TenantUserId`

6. **RecordPayments.cshtml.cs**
   - Replace `Bill.TenantId` with `Bill.TenantUserId`
   - Update all queries and ViewModels

#### Medium Priority
7. **Tenant Pages** (SubmitRequest, Messages, Documents, etc.)
   - Replace `User.Tenant` navigation with direct User properties
   - Use `User.ApartmentId`, `User.LeaseStart`, etc.

8. **InvoicePdfService.cs**
   - Replace `Invoice.Tenant` with `Invoice.TenantUser`
   - Update PDF generation to use User properties

---

## Step D: Authorization - 2-Role Enforcement ✅

### Changes Made

#### 1. Role Enum Updated
- Manager role removed from `UserRoles` enum
- Only Admin and User roles remain

#### 2. Authorization Attributes Updated
- All `[Authorize(Roles = "Manager")]` → `[Authorize(Roles = "Admin")]`
- All `[Authorize(Roles = "Admin,Manager")]` → `[Authorize(Roles = "Admin")]`
- Tenant pages: `[Authorize(Roles = "User")]` (unchanged)

#### 3. Navigation Updated
- Manager sidebar updated to use Admin role
- Manager menu items should be moved to Admin area

**Remaining Work:**
- Update navigation menus to remove Manager-specific items
- Ensure all Manager functionality accessible via Admin area

---

## Step E: Admin Impersonation ⏳

### Status: **NOT IMPLEMENTED**

### Required Implementation

1. **Impersonation Action**
   - Add "Impersonate" button on ManageUsers page
   - Store original Admin ID in session/claims
   - Issue temporary session as target user
   - Log impersonation in AuditLogs

2. **Stop Impersonation**
   - Clear impersonation session
   - Restore original Admin session
   - Log end of impersonation

3. **Security Considerations**
   - Only Admin role can impersonate
   - Log all impersonation actions
   - Consider MFA requirement
   - Time-limited sessions

---

## Step F: Update Client-Side Routes ⏳

### Status: **PENDING**

### Required Updates

1. **JavaScript/AJAX Calls**
   - Update any `/api/tenant/...` endpoints to `/api/user/...`
   - Update field names in client-side validation
   - Update form field bindings

2. **Razor View Updates**
   - Update `asp-for` bindings to use User properties
   - Update display templates
   - Update validation messages

---

## Step G: Tests & QA ⏳

### Status: **PENDING**

### Required Testing

#### Unit Tests
- [ ] Update test fixtures to create Users instead of Tenants
- [ ] Test Admin Create User flow
- [ ] Test authorization (Admin vs User)
- [ ] Test bill generation with Users
- [ ] Test payment recording with TenantUserId

#### Integration Tests
- [ ] Admin creates user
- [ ] User views their bills
- [ ] User makes payment
- [ ] Admin generates bills
- [ ] Admin records payments

#### Manual QA Checklist
- [ ] Admin can create user
- [ ] User can log in and view dashboard
- [ ] User can view bills and invoices
- [ ] User can make payments
- [ ] Admin can generate bills
- [ ] Admin can record payments
- [ ] Public registration is disabled
- [ ] Manager role no longer accessible
- [ ] All Manager features accessible to Admin

---

## Step H: Final Cleanup ⏳

### Status: **NOT STARTED** (Do not perform until QA passes)

### Preconditions
- ✅ All code compiles
- ⏳ All tests pass
- ⏳ Manual QA completed
- ⏳ No runtime references to Tenants.*
- ⏳ Database backup taken

### Cleanup Tasks
1. Drop Tenants table (SQL script)
2. Remove Tenant model file
3. Remove obsolete properties from User, Bill, Invoice models
4. Remove obsolete DbSet from ApplicationDbContext
5. Remove obsolete migration artifacts
6. Clean up comments and documentation

---

## Build Status

### Current Build
✅ **SUCCESS** - No compilation errors

### Warnings
- Obsolete property warnings (expected during migration)
- Manager role references in some views (non-critical)

---

## Files Changed Summary

### Step A Completed
- ✅ `Pages/Register.cshtml.cs` - Disabled public registration
- ✅ `Enums/UserRoles.cs` - Removed Manager role
- ✅ `Pages/Manager/*.cshtml.cs` (7 files) - Updated authorization
- ✅ `Pages/ManageApartments.cshtml.cs` - Updated authorization
- ✅ `Pages/Login.cshtml.cs` - Updated redirect logic
- ✅ `Pages/Shared/_ManagerSidebar.cshtml` - Updated default role
- ✅ `Pages/Admin/ManageUsers.cshtml` - Removed Manager references

### Step B Pending
- ⏳ `Pages/Admin/Users/Create.cshtml` - Create user page
- ⏳ `Pages/Admin/Users/Create.cshtml.cs` - Create user logic
- ⏳ `Model/InviteToken.cs` - Invite token model (optional)

### Step C Pending
- ⏳ Multiple Tenant pages need refactoring (see list above)

### Step D Completed
- ✅ All authorization attributes updated
- ✅ Role enum updated

### Step E Pending
- ⏳ Impersonation functionality

### Step F Pending
- ⏳ Client-side route updates

### Step G Pending
- ⏳ Test updates and execution

### Step H Pending
- ⏳ Final cleanup (after QA)

---

## Critical Remaining Work

### Must Complete Before PR
1. **Create Admin Create User Page** (Step B)
2. **Refactor Critical Tenant Pages** (Step C - at least TenantDashboard, MakePayment, GenerateBills)
3. **Update Tests** (Step G)
4. **Manual QA** (Step G)

### Can Defer to Future PRs
1. Full invite token system (Step B enhancement)
2. Admin impersonation (Step E)
3. Complete Tenant page refactoring (Step C - lower priority pages)
4. Final cleanup (Step H - after QA)

---

## Migration Notes

### Database Considerations
- Existing Manager users in database should be migrated to Admin role
- Existing Tenant records still in database (will be dropped in Step H)
- Old `TenantId` columns still exist (will be dropped in Step H)

### Backward Compatibility
- Obsolete properties kept for migration safety
- Old Tenant references generate warnings but don't break compilation
- Can gradually migrate pages without breaking existing functionality

---

## Next Steps

1. **Immediate**: Create Admin Create User page
2. **High Priority**: Refactor TenantDashboard, MakePayment, GenerateBills
3. **Medium Priority**: Update remaining Tenant pages
4. **Before Merge**: Complete tests and QA
5. **After QA**: Perform final cleanup (Step H)

---

**Status**: ⚠️ **WORK IN PROGRESS**
**Ready for PR**: ❌ **NO** - Critical work remaining
**Estimated Completion**: After Step B and critical Step C pages completed

