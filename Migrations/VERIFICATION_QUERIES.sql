-- ====================================================
-- VERIFICATION QUERIES FOR TENANT-TO-USER MIGRATION
-- ====================================================
-- Run these queries AFTER executing the migration script
-- All critical checks must return 0 before proceeding
-- ====================================================

USE [apartmentDB];
GO

-- ====================================================
-- CRITICAL VERIFICATION CHECKS
-- ====================================================
-- These MUST all return 0 for the migration to be considered successful
-- ====================================================

PRINT '========================================';
PRINT 'CRITICAL VERIFICATION CHECKS';
PRINT '========================================';
PRINT '';

-- 1. Bills: Check for NULL TenantUserId
PRINT '1. Bills with NULL TenantUserId (MUST BE 0):';
SELECT COUNT(*) AS NullCount
FROM Bills 
WHERE TenantUserId IS NULL;
GO

-- 2. Invoices: Check for NULL TenantUserId
PRINT '2. Invoices with NULL TenantUserId (MUST BE 0):';
SELECT COUNT(*) AS NullCount
FROM Invoices 
WHERE TenantUserId IS NULL;
GO

-- 3. Requests: Check for NULL SubmittedByUserId (informational)
PRINT '3. Requests with NULL SubmittedByUserId (informational):';
SELECT COUNT(*) AS NullCount
FROM Requests 
WHERE SubmittedByUserId IS NULL;
GO

-- 4. Bills: Check for invalid TenantUserId references
PRINT '4. Bills with invalid TenantUserId (MUST BE 0):';
SELECT COUNT(*) AS InvalidCount
FROM Bills B
LEFT JOIN Users U ON B.TenantUserId = U.Id
WHERE B.TenantUserId IS NOT NULL AND U.Id IS NULL;
GO

-- 5. Invoices: Check for invalid TenantUserId references
PRINT '5. Invoices with invalid TenantUserId (MUST BE 0):';
SELECT COUNT(*) AS InvalidCount
FROM Invoices I
LEFT JOIN Users U ON I.TenantUserId = U.Id
WHERE I.TenantUserId IS NOT NULL AND U.Id IS NULL;
GO

-- ====================================================
-- DATA INTEGRITY CHECKS
-- ====================================================

PRINT '';
PRINT '========================================';
PRINT 'DATA INTEGRITY CHECKS';
PRINT '========================================';
PRINT '';

-- 6. Verify tenant data was copied to Users
PRINT '6. Users with tenant data populated:';
SELECT 
    COUNT(*) AS TotalUsers,
    COUNT(ApartmentId) AS UsersWithApartmentId,
    COUNT(LeaseStart) AS UsersWithLeaseStart,
    COUNT(LeaseEnd) AS UsersWithLeaseEnd,
    COUNT(LeaseStatus) AS UsersWithLeaseStatus
FROM Users;
GO

-- 7. Compare tenant count vs users with tenant data
PRINT '7. Tenant vs User comparison:';
SELECT 
    (SELECT COUNT(*) FROM Tenants) AS TotalTenants,
    (SELECT COUNT(*) FROM Users WHERE ApartmentId IS NOT NULL OR LeaseStatus IS NOT NULL) AS UsersWithTenantData;
GO

-- ====================================================
-- SAMPLE VERIFICATION
-- ====================================================
-- Manually verify 10 former tenants
-- ====================================================

PRINT '';
PRINT '========================================';
PRINT 'SAMPLE VERIFICATION - 10 TENANTS';
PRINT '========================================';
PRINT '';

SELECT TOP 10
    T.Id AS TenantId,
    T.FullName AS TenantName,
    T.PrimaryEmail AS TenantEmail,
    T.ApartmentId AS TenantApartmentId,
    T.Status AS TenantStatus,
    U.Id AS UserId,
    U.Email AS UserEmail,
    U.ApartmentId AS UserApartmentId,
    U.LeaseStatus AS UserLeaseStatus,
    (SELECT COUNT(*) FROM Bills WHERE TenantUserId = U.Id) AS BillCount,
    (SELECT COUNT(*) FROM Invoices WHERE TenantUserId = U.Id) AS InvoiceCount,
    CASE 
        WHEN U.Id IS NULL THEN 'NO USER FOUND'
        WHEN U.ApartmentId = T.ApartmentId AND U.LeaseStatus = CAST(T.Status AS NVARCHAR(32)) THEN 'MATCH'
        ELSE 'MISMATCH'
    END AS DataMatchStatus
FROM Tenants T
LEFT JOIN Users U ON (
    U.TenantID = T.Id 
    OR LOWER(U.Email) = LOWER(T.PrimaryEmail)
)
ORDER BY T.Id;
GO

-- ====================================================
-- FOREIGN KEY VERIFICATION
-- ====================================================

PRINT '';
PRINT '========================================';
PRINT 'FOREIGN KEY VERIFICATION';
PRINT '========================================';
PRINT '';

-- Check that FK constraints exist
SELECT 
    OBJECT_NAME(f.parent_object_id) AS TableName,
    f.name AS ForeignKeyName,
    OBJECT_NAME(f.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys f
WHERE f.name IN (
    'FK_Bills_Users_TenantUserId',
    'FK_Invoices_Users_TenantUserId'
)
ORDER BY TableName;
GO

-- ====================================================
-- MIGRATION SUMMARY
-- ====================================================

PRINT '';
PRINT '========================================';
PRINT 'MIGRATION SUMMARY STATISTICS';
PRINT '========================================';
PRINT '';

SELECT 
    'Users with tenant data' AS Metric,
    COUNT(*) AS Count
FROM Users
WHERE ApartmentId IS NOT NULL OR LeaseStatus IS NOT NULL
UNION ALL
SELECT 
    'Bills with TenantUserId',
    COUNT(*)
FROM Bills
WHERE TenantUserId IS NOT NULL
UNION ALL
SELECT 
    'Invoices with TenantUserId',
    COUNT(*)
FROM Invoices
WHERE TenantUserId IS NOT NULL
UNION ALL
SELECT 
    'Total Tenants (original)',
    COUNT(*)
FROM Tenants
UNION ALL
SELECT 
    'Total Users',
    COUNT(*)
FROM Users;
GO

PRINT '';
PRINT '========================================';
PRINT 'VERIFICATION COMPLETE';
PRINT '========================================';
PRINT 'Review all results above.';
PRINT 'All critical checks (1-5) must return 0.';
PRINT '========================================';
GO

