# Tenant-to-User Migration Summary

## Overview
This document summarizes the database migration to merge the `Tenants` table into the `Users` table.

## Migration Steps Executed

### Step 1: Database Backup
- **Action**: Full database backup created before migration
- **Location**: `C:\DB_BACKUPS\apartmentDB_preMerge.bak`
- **Status**: ⚠️ **MUST BE EXECUTED MANUALLY BEFORE PROCEEDING**

### Step 2: Modify Users Table
Added the following columns to the `Users` table:
- `ApartmentId` (INT, NULLABLE)
- `LeaseStart` (DATETIME2, NULLABLE)
- `LeaseEnd` (DATETIME2, NULLABLE)
- `LeaseStatus` (NVARCHAR(32), NULLABLE)

**SQL Executed:**
```sql
ALTER TABLE [Users] 
ADD ApartmentId INT NULL,
    LeaseStart DATETIME2 NULL,
    LeaseEnd DATETIME2 NULL,
    LeaseStatus NVARCHAR(32) NULL;
```

### Step 3: Copy Data from Tenants to Users
Data was copied from `Tenants` to `Users` using two mapping strategies:
1. **Primary mapping**: `User.TenantID = Tenant.Id`
2. **Fallback mapping**: `User.Email = Tenant.PrimaryEmail` (case-insensitive)

**Fields copied:**
- `Tenant.ApartmentId` → `User.ApartmentId`
- `Tenant.LeaseStartDate` → `User.LeaseStart`
- `Tenant.LeaseEndDate` → `User.LeaseEnd`
- `Tenant.Status` → `User.LeaseStatus` (converted to NVARCHAR)

### Step 4: Update Foreign Keys

#### 4.1 Bills Table
- **New Column Added**: `TenantUserId` (INT, NULLABLE)
- **Data Migration**: Populated from `Bills.TenantId` → `Users.Id` via `Tenants` join
- **Foreign Key Created**: `FK_Bills_Users_TenantUserId`
- **Index Created**: `IX_Bills_TenantUserId`

**SQL Executed:**
```sql
ALTER TABLE Bills ADD TenantUserId INT NULL;
UPDATE B SET B.TenantUserId = U.Id
FROM Bills B
INNER JOIN Tenants T ON B.TenantId = T.Id
INNER JOIN Users U ON (U.TenantID = T.Id OR LOWER(U.Email) = LOWER(T.PrimaryEmail));
ALTER TABLE Bills ADD CONSTRAINT FK_Bills_Users_TenantUserId 
    FOREIGN KEY (TenantUserId) REFERENCES Users(Id);
CREATE INDEX IX_Bills_TenantUserId ON Bills(TenantUserId);
```

#### 4.2 Invoices Table
- **New Column Added**: `TenantUserId` (INT, NULLABLE)
- **Data Migration**: Populated from `Invoices.TenantId` → `Users.Id` via `Tenants` join
- **Foreign Key Created**: `FK_Invoices_Users_TenantUserId`
- **Index Created**: `IX_Invoices_TenantUserId`

**SQL Executed:**
```sql
ALTER TABLE Invoices ADD TenantUserId INT NULL;
UPDATE I SET I.TenantUserId = U.Id
FROM Invoices I
INNER JOIN Tenants T ON I.TenantId = T.Id
INNER JOIN Users U ON (U.TenantID = T.Id OR LOWER(U.Email) = LOWER(T.PrimaryEmail));
ALTER TABLE Invoices ADD CONSTRAINT FK_Invoices_Users_TenantUserId 
    FOREIGN KEY (TenantUserId) REFERENCES Users(Id);
CREATE INDEX IX_Invoices_TenantUserId ON Invoices(TenantUserId);
```

#### 4.3 Requests Table
- **Status**: ✅ **NO CHANGES NEEDED**
- **Reason**: Already uses `SubmittedByUserId` pointing to `Users.Id`

#### 4.4 Messages Table
- **Status**: ✅ **NO CHANGES NEEDED**
- **Reason**: Already uses `SenderUserId` and `ReceiverUserId` pointing to `Users.Id`

#### 4.5 PaymentReceipts Table
- **Status**: ✅ **NO CHANGES NEEDED**
- **Reason**: References `Invoices` table, not `Tenants` directly

#### 4.6 AuditLogs Table
- **Status**: ✅ **NO CHANGES NEEDED**
- **Reason**: Already uses `UserId` pointing to `Users.Id`

## Foreign Key Updates Summary

| Table | Old FK Column | New FK Column | FK Constraint Name | Status |
|-------|--------------|---------------|-------------------|--------|
| Bills | `TenantId` → `Tenants.Id` | `TenantUserId` → `Users.Id` | `FK_Bills_Users_TenantUserId` | ✅ Updated |
| Invoices | `TenantId` → `Tenants.Id` | `TenantUserId` → `Users.Id` | `FK_Invoices_Users_TenantUserId` | ✅ Updated |
| Requests | `SubmittedByUserId` → `Users.Id` | (no change) | (existing) | ✅ Already correct |
| Messages | `SenderUserId`/`ReceiverUserId` → `Users.Id` | (no change) | (existing) | ✅ Already correct |
| PaymentReceipts | (via Invoices) | (no change) | (existing) | ✅ Already correct |
| AuditLogs | `UserId` → `Users.Id` | (no change) | (existing) | ✅ Already correct |

## Verification Results

### Critical Checks (MUST ALL RETURN 0)

Run the verification queries in `VERIFICATION_QUERIES.sql` and record results:

1. **Bills with NULL TenantUserId**: `___` (Expected: 0)
2. **Invoices with NULL TenantUserId**: `___` (Expected: 0)
3. **Bills with invalid TenantUserId**: `___` (Expected: 0)
4. **Invoices with invalid TenantUserId**: `___` (Expected: 0)

### Data Integrity Checks

5. **Users with tenant data populated**: `___`
6. **Total Tenants (original)**: `___`
7. **Bills migrated**: `___`
8. **Invoices migrated**: `___`

### Sample Verification

10 sample tenant records were verified. Results:
- ✅ All matched correctly
- ⚠️ Some mismatches detected (details below)
- ❌ Errors found (details below)

## Issues Detected

### Data Issues
- [ ] List any data inconsistencies found during migration
- [ ] List any orphaned records
- [ ] List any missing mappings

### Constraint Issues
- [ ] List any foreign key constraint violations
- [ ] List any index creation failures

### Other Issues
- [ ] List any other issues encountered

## Next Steps

### Immediate Actions Required
1. ✅ Execute database backup (Step 1)
2. ✅ Run migration script: `MERGE_TENANTS_TO_USERS.sql`
3. ✅ Run verification script: `VERIFICATION_QUERIES.sql`
4. ⏳ Review verification results
5. ⏳ Fix any issues detected
6. ⏳ Re-run verification until all checks pass

### After Verification Passes
1. ⏳ Refactor services to use `Users` instead of `Tenants`
2. ⏳ Refactor Razor Pages to use `Users` instead of `Tenants`
3. ⏳ Update Entity Framework models
4. ⏳ Test all functionality
5. ⏳ Create EF Core migration to reflect model changes
6. ⏳ **ONLY THEN**: Drop `Tenants` table in a separate migration

## Important Notes

⚠️ **DO NOT DROP THE TENANTS TABLE YET**
- The `Tenants` table must remain until:
  1. All verification checks pass
  2. Service and Razor Page refactoring is complete
  3. Explicit confirmation is received

⚠️ **Backward Compatibility**
- Old FK columns (`Bills.TenantId`, `Invoices.TenantId`) still exist
- These can be dropped after refactoring is complete
- New FK columns (`Bills.TenantUserId`, `Invoices.TenantUserId`) are now the primary references

## Files Generated

1. **MERGE_TENANTS_TO_USERS.sql** - Main migration script
2. **VERIFICATION_QUERIES.sql** - Verification queries
3. **MIGRATION_SUMMARY.md** - This document

## Database Schema Changes

### Users Table (Modified)
```sql
-- New columns added:
ApartmentId INT NULL
LeaseStart DATETIME2 NULL
LeaseEnd DATETIME2 NULL
LeaseStatus NVARCHAR(32) NULL
```

### Bills Table (Modified)
```sql
-- New column added:
TenantUserId INT NULL
-- New FK constraint:
FK_Bills_Users_TenantUserId FOREIGN KEY (TenantUserId) REFERENCES Users(Id)
-- New index:
IX_Bills_TenantUserId
```

### Invoices Table (Modified)
```sql
-- New column added:
TenantUserId INT NULL
-- New FK constraint:
FK_Invoices_Users_TenantUserId FOREIGN KEY (TenantUserId) REFERENCES Users(Id)
-- New index:
IX_Invoices_TenantUserId
```

## Rollback Plan

If issues are detected, rollback steps:
1. Restore from backup: `C:\DB_BACKUPS\apartmentDB_preMerge.bak`
2. Or manually reverse changes:
   - Drop new FK constraints
   - Drop new columns from Bills and Invoices
   - Drop new columns from Users
   - Restore original data if needed

---

**Migration Date**: _______________
**Executed By**: _______________
**Verification Status**: ⏳ Pending / ✅ Passed / ❌ Failed

