-- Migration RemoveFinSchemaObjects: drops what AddGmcDbObjects put in the FIN schema
-- (2 procedures, 3 tables with their data), then the schema itself. SERP and FW are kept; neither refers to FIN.
-- Batches are separated by GO; each one is safe to re-run.

DROP PROCEDURE IF EXISTS [FIN].[USP_Reg_FIN_Customer];
GO

DROP PROCEDURE IF EXISTS [FIN].[USP_IU_Given_Recieve];
GO

DROP TABLE IF EXISTS [FIN].[tblAmountGiven];
GO

DROP TABLE IF EXISTS [FIN].[tblAmountRecieved];
GO

DROP TABLE IF EXISTS [FIN].[tblFinCustomers];
GO

IF SCHEMA_ID(N'FIN') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'FIN')) AND NOT EXISTS (SELECT 1 FROM sys.types WHERE schema_id = SCHEMA_ID(N'FIN'))
    EXEC(N'DROP SCHEMA [FIN]');
GO
