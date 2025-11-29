# Quick Reference: Tenant-to-User Migration

## Files Created

1. **MERGE_TENANTS_TO_USERS.sql** - Execute this first (main migration)
2. **VERIFICATION_QUERIES.sql** - Execute this after migration (verification)
3. **MIGRATION_SUMMARY.md** - Detailed documentation
4. **QUICK_REFERENCE.md** - This file

## Execution Order

### 1. Backup Database (MANUAL)
```sql
BACKUP DATABASE [apartmentDB] 
TO DISK = 'C:\DB_BACKUPS\apartmentDB_preMerge.bak'
WITH FORMAT, INIT;
```

### 2. Run Migration
Execute: `MERGE_TENANTS_TO_USERS.sql`

### 3. Verify Migration
Execute: `VERIFICATION_QUERIES.sql`

### 4. Review Results
- All critical checks must return **0**
- Review sample verification data
- Check summary statistics

## Critical Verification Checks

These **MUST** all return **0**:

```sql
-- 1. Bills with NULL TenantUserId
SELECT COUNT(*) FROM Bills WHERE TenantUserId IS NULL;

-- 2. Invoices with NULL TenantUserId  
SELECT COUNT(*) FROM Invoices WHERE TenantUserId IS NULL;

-- 3. Bills with invalid TenantUserId
SELECT COUNT(*) FROM Bills B
LEFT JOIN Users U ON B.TenantUserId = U.Id
WHERE B.TenantUserId IS NOT NULL AND U.Id IS NULL;

-- 4. Invoices with invalid TenantUserId
SELECT COUNT(*) FROM Invoices I
LEFT JOIN Users U ON I.TenantUserId = U.Id
WHERE I.TenantUserId IS NOT NULL AND U.Id IS NULL;
```

## What Changed

### Users Table
- ✅ Added: `ApartmentId`, `LeaseStart`, `LeaseEnd`, `LeaseStatus`
- ✅ Data copied from `Tenants` table

### Bills Table
- ✅ Added: `TenantUserId` column
- ✅ New FK: `FK_Bills_Users_TenantUserId`
- ⚠️ Old column `TenantId` still exists (will be dropped later)

### Invoices Table
- ✅ Added: `TenantUserId` column
- ✅ New FK: `FK_Invoices_Users_TenantUserId`
- ⚠️ Old column `TenantId` still exists (will be dropped later)

### Tenants Table
- ⚠️ **NOT DROPPED** - Preserved for now

## Mapping Strategy

Data was copied from `Tenants` to `Users` using:
1. **Primary**: `User.TenantID = Tenant.Id`
2. **Fallback**: `User.Email = Tenant.PrimaryEmail` (case-insensitive)

## Next Steps After Verification

1. ✅ Migration complete
2. ⏳ Refactor services (use `Users` instead of `Tenants`)
3. ⏳ Refactor Razor Pages (use `Users` instead of `Tenants`)
4. ⏳ Update EF Core models
5. ⏳ Test all functionality
6. ⏳ Create EF Core migration
7. ⏳ Drop `Tenants` table (separate migration)

## Rollback

If needed, restore from:
```
C:\DB_BACKUPS\apartmentDB_preMerge.bak
```

---

**Status**: Ready for execution
**Backup Required**: YES (manual step)
**Tenants Table**: Preserved (not dropped)

