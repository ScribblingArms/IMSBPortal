-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- User-defined table types

IF TYPE_ID(N'gmc.BeneficiaryAttendanceTVP') IS NULL
    EXEC(N'CREATE TYPE [gmc].[BeneficiaryAttendanceTVP] AS TABLE(
	[BeneficiaryID] [int] NOT NULL
)');
GO
