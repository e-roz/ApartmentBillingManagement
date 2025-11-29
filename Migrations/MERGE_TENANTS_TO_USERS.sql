-- ====================================================
-- SAFE DATABASE MIGRATION: MERGE TENANTS INTO USERS
-- ====================================================
-- This script merges the Tenants table into the Users table
-- Follow each step EXACTLY in order
-- DO NOT drop Tenants table until verification is complete
-- ====================================================

USE [apartmentDB];
GO

-- ====================================================
-- STEP 1 — PREPARE & BACKUP
-- ====================================================
-- Create a full backup of the database BEFORE any changes
-- ====================================================

-- Ensure backup directory exists (adjust path as needed)
-- BACKUP DATABASE [apartmentDB] 
-- TO DISK = 'C:\DB_BACKUPS\apartmentDB_preMerge.bak'
-- WITH FORMAT, INIT, NAME = 'apartmentDB Pre-Merge Backup', 
--      DESCRIPTION = 'Full backup before merging Tenants into Users';
-- GO

-- NOTE: Uncomment the BACKUP command above and execute it manually
-- Verify the backup file exists before proceeding to Step 2
-- ====================================================


-- ====================================================
-- STEP 2 — MODIFY USERS TABLE
-- ====================================================
-- Add new columns to the Users table to store tenant data
-- ====================================================

ALTER TABLE [Users] 
ADD ApartmentId INT NULL,
    LeaseStart DATETIME2 NULL,
    LeaseEnd DATETIME2 NULL,
    LeaseStatus NVARCHAR(32) NULL;
GO

-- Verify columns were added
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users' 
    AND COLUMN_NAME IN ('ApartmentId', 'LeaseStart', 'LeaseEnd', 'LeaseStatus');
GO

-- ====================================================
-- STEP 3 — COPY DATA FROM TENANTS INTO USERS
-- ====================================================
-- Copy tenant data to Users table
-- Mapping: User.TenantID = Tenant.Id OR User.Email = Tenant.PrimaryEmail
-- ====================================================

SET QUOTED_IDENTIFIER ON;
GO

-- First, update Users where User.TenantID matches Tenant.Id
UPDATE U
SET U.ApartmentId = T.ApartmentId,
    U.LeaseStart = T.LeaseStartDate,
    U.LeaseEnd = T.LeaseEndDate,
    U.LeaseStatus = CAST(T.Status AS NVARCHAR(32))
FROM Users U
INNER JOIN Tenants T ON U.TenantID = T.Id
WHERE U.TenantID IS NOT NULL;
GO

-- Second, update Users where emails match (for users not linked via TenantID)
SET QUOTED_IDENTIFIER ON;
GO

UPDATE U
SET U.ApartmentId = T.ApartmentId,
    U.LeaseStart = T.LeaseStartDate,
    U.LeaseEnd = T.LeaseEndDate,
    U.LeaseStatus = CAST(T.Status AS NVARCHAR(32))
FROM Users U
INNER JOIN Tenants T ON LOWER(U.Email) = LOWER(T.PrimaryEmail)
WHERE U.TenantID IS NULL  -- Only update if not already updated above
    AND U.ApartmentId IS NULL;  -- Only if not already populated
GO

-- Verify data was copied
SELECT 
    COUNT(*) AS TotalUsers,
    COUNT(ApartmentId) AS UsersWithApartmentId,
    COUNT(LeaseStart) AS UsersWithLeaseStart,
    COUNT(LeaseStatus) AS UsersWithLeaseStatus
FROM Users;
GO

-- ====================================================
-- STEP 4 — UPDATE FOREIGN KEYS TO POINT TO USERS
-- ====================================================
-- Tables that reference Tenants.Id must be updated to reference Users.Id
-- ====================================================

-- ====================================================
-- 4.1 — BILLS TABLE
-- ====================================================

-- Step 4.1.1: Add new FK column
ALTER TABLE Bills 
ADD TenantUserId INT NULL;
GO

-- Step 4.1.2: Populate new FK with Users.Id
SET QUOTED_IDENTIFIER ON;
GO

UPDATE B
SET B.TenantUserId = U.Id
FROM Bills B
INNER JOIN Tenants T ON B.TenantId = T.Id
INNER JOIN Users U ON (
    U.TenantID = T.Id 
    OR LOWER(U.Email) = LOWER(T.PrimaryEmail)
);
GO

-- Step 4.1.3: Create new FK constraint
ALTER TABLE Bills 
ADD CONSTRAINT FK_Bills_Users_TenantUserId 
FOREIGN KEY (TenantUserId) REFERENCES Users(Id);
GO

-- Create index for performance
CREATE INDEX IX_Bills_TenantUserId ON Bills(TenantUserId);
GO

-- ====================================================
-- 4.2 — INVOICES TABLE
-- ====================================================

-- Step 4.2.1: Add new FK column
ALTER TABLE Invoices 
ADD TenantUserId INT NULL;
GO

-- Step 4.2.2: Populate new FK with Users.Id
SET QUOTED_IDENTIFIER ON;
GO

UPDATE I
SET I.TenantUserId = U.Id
FROM Invoices I
INNER JOIN Tenants T ON I.TenantId = T.Id
INNER JOIN Users U ON (
    U.TenantID = T.Id 
    OR LOWER(U.Email) = LOWER(T.PrimaryEmail)
);
GO

-- Step 4.2.3: Create new FK constraint
ALTER TABLE Invoices 
ADD CONSTRAINT FK_Invoices_Users_TenantUserId 
FOREIGN KEY (TenantUserId) REFERENCES Users(Id);
GO

-- Create index for performance
CREATE INDEX IX_Invoices_TenantUserId ON Invoices(TenantUserId);
GO

-- ====================================================
-- 4.3 — REQUESTS TABLE
-- ====================================================
-- NOTE: Requests already has SubmittedByUserId pointing to Users.Id
-- No changes needed for Requests table
-- ====================================================

-- ====================================================
-- 4.4 — MESSAGES TABLE
-- ====================================================
-- NOTE: Messages already has SenderUserId and ReceiverUserId pointing to Users.Id
-- No changes needed for Messages table
-- ====================================================

-- ====================================================
-- 4.5 — PAYMENTRECEIPTS TABLE
-- ====================================================
-- NOTE: PaymentReceipts references Invoices, not Tenants directly
-- No changes needed for PaymentReceipts table
-- ====================================================

-- ====================================================
-- 4.6 — AUDITLOGS TABLE
-- ====================================================
-- NOTE: AuditLogs already has UserId pointing to Users.Id
-- No changes needed for AuditLogs table
-- ====================================================


-- ====================================================
-- STEP 5 — VERIFICATION (CRITICAL)
-- ====================================================
-- Run these checks to ensure all data was migrated correctly
-- ALL counts must return 0 before proceeding
-- ====================================================

PRINT '========================================';
PRINT 'VERIFICATION CHECKS';
PRINT '========================================';
GO

-- 1. Bills: Check for NULL TenantUserId
DECLARE @BillsNullCount INT;
SELECT @BillsNullCount = COUNT(*) 
FROM Bills 
WHERE TenantUserId IS NULL;

IF @BillsNullCount > 0
    PRINT 'WARNING: Bills table has ' + CAST(@BillsNullCount AS VARCHAR(10)) + ' records with NULL TenantUserId';
ELSE
    PRINT 'PASS: All Bills have TenantUserId populated';
GO

-- 2. Invoices: Check for NULL TenantUserId
DECLARE @InvoicesNullCount INT;
SELECT @InvoicesNullCount = COUNT(*) 
FROM Invoices 
WHERE TenantUserId IS NULL;

IF @InvoicesNullCount > 0
    PRINT 'WARNING: Invoices table has ' + CAST(@InvoicesNullCount AS VARCHAR(10)) + ' records with NULL TenantUserId';
ELSE
    PRINT 'PASS: All Invoices have TenantUserId populated';
GO

-- 3. Requests: Check for NULL SubmittedByUserId (should already be OK)
DECLARE @RequestsNullCount INT;
SELECT @RequestsNullCount = COUNT(*) 
FROM Requests 
WHERE SubmittedByUserId IS NULL;

IF @RequestsNullCount > 0
    PRINT 'INFO: Requests table has ' + CAST(@RequestsNullCount AS VARCHAR(10)) + ' records with NULL SubmittedByUserId (may be acceptable)';
ELSE
    PRINT 'PASS: All Requests have SubmittedByUserId populated';
GO

-- 4. Sample verification: Check 10 former tenants
PRINT '';
PRINT 'Sample Verification - First 10 Tenants:';
SELECT TOP 10
    T.Id AS TenantId,
    T.PrimaryEmail AS TenantEmail,
    U.Id AS UserId,
    U.Email AS UserEmail,
    U.ApartmentId,
    U.LeaseStatus,
    (SELECT COUNT(*) FROM Bills WHERE TenantUserId = U.Id) AS BillCount,
    (SELECT COUNT(*) FROM Invoices WHERE TenantUserId = U.Id) AS InvoiceCount
FROM Tenants T
LEFT JOIN Users U ON (U.TenantID = T.Id OR LOWER(U.Email) = LOWER(T.PrimaryEmail))
ORDER BY T.Id;
GO

-- 5. Data integrity check: Verify all Bills point to valid Users
DECLARE @BillsInvalidCount INT;
SELECT @BillsInvalidCount = COUNT(*) 
FROM Bills B
LEFT JOIN Users U ON B.TenantUserId = U.Id
WHERE B.TenantUserId IS NOT NULL AND U.Id IS NULL;

IF @BillsInvalidCount > 0
    PRINT 'ERROR: Bills table has ' + CAST(@BillsInvalidCount AS VARCHAR(10)) + ' records with invalid TenantUserId';
ELSE
    PRINT 'PASS: All Bills TenantUserId values are valid';
GO

-- 6. Data integrity check: Verify all Invoices point to valid Users
DECLARE @InvoicesInvalidCount INT;
SELECT @InvoicesInvalidCount = COUNT(*) 
FROM Invoices I
LEFT JOIN Users U ON I.TenantUserId = U.Id
WHERE I.TenantUserId IS NOT NULL AND U.Id IS NULL;

IF @InvoicesInvalidCount > 0
    PRINT 'ERROR: Invoices table has ' + CAST(@InvoicesInvalidCount AS VARCHAR(10)) + ' records with invalid TenantUserId';
ELSE
    PRINT 'PASS: All Invoices TenantUserId values are valid';
GO

-- 7. Summary statistics
PRINT '';
PRINT '========================================';
PRINT 'MIGRATION SUMMARY';
PRINT '========================================';

SELECT 
    'Users with tenant data' AS Metric,
    COUNT(*) AS Count
FROM Users
WHERE ApartmentId IS NOT NULL OR LeaseStatus IS NOT NULL
UNION ALL
SELECT 
    'Bills migrated',
    COUNT(*)
FROM Bills
WHERE TenantUserId IS NOT NULL
UNION ALL
SELECT 
    'Invoices migrated',
    COUNT(*)
FROM Invoices
WHERE TenantUserId IS NOT NULL;
GO

-- ====================================================
-- STEP 6 — DO NOT DROP TENANTS YET
-- ====================================================
-- The Tenants table should remain untouched until:
-- 1. All verification checks pass (all counts = 0)
-- 2. Service and Razor Page refactoring is complete
-- 3. Explicit confirmation is received
-- ====================================================

PRINT '';
PRINT '========================================';
PRINT 'MIGRATION COMPLETE - TENANTS TABLE PRESERVED';
PRINT '========================================';
PRINT 'Next steps:';
PRINT '1. Review all verification results above';
PRINT '2. Ensure all counts are 0 or acceptable';
PRINT '3. Refactor services and Razor Pages to use Users instead of Tenants';
PRINT '4. After refactoring, drop Tenants table in a separate migration';
PRINT '========================================';
GO

