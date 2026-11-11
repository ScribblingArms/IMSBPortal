-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Schemas gmc (the dbo objects of gmc_db), FIN, FW, SERP

IF SCHEMA_ID(N'gmc') IS NULL
    EXEC(N'CREATE SCHEMA [gmc]');
GO

IF SCHEMA_ID(N'FIN') IS NULL
    EXEC(N'CREATE SCHEMA [FIN]');
GO

IF SCHEMA_ID(N'FW') IS NULL
    EXEC(N'CREATE SCHEMA [FW]');
GO

IF SCHEMA_ID(N'SERP') IS NULL
    EXEC(N'CREATE SCHEMA [SERP]');
GO
