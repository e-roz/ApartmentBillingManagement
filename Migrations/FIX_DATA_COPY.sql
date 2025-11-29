-- Fix script to copy tenant data to Users table
USE [apartmentDB];
GO

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
UPDATE U
SET U.ApartmentId = T.ApartmentId,
    U.LeaseStart = T.LeaseStartDate,
    U.LeaseEnd = T.LeaseEndDate,
    U.LeaseStatus = CAST(T.Status AS NVARCHAR(32))
FROM Users U
INNER JOIN Tenants T ON LOWER(U.Email) = LOWER(T.PrimaryEmail)
WHERE (U.TenantID IS NULL OR U.ApartmentId IS NULL)
    AND NOT EXISTS (
        SELECT 1 FROM Users U2 
        WHERE U2.Id = U.Id AND U2.ApartmentId IS NOT NULL
    );
GO

-- Verify data was copied
SELECT 
    COUNT(*) AS TotalUsers,
    COUNT(ApartmentId) AS UsersWithApartmentId,
    COUNT(LeaseStart) AS UsersWithLeaseStart,
    COUNT(LeaseStatus) AS UsersWithLeaseStatus
FROM Users;
GO

