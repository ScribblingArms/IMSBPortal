-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Views

CREATE OR ALTER VIEW [gmc].[vw_RCTesting]
AS
select a.RCTestingID,a.ELSRID,a.Description,a.CreatedBy as 'EMPID' ,(select Section from [gmc].[TblEMPUser] b where b.EMPID=a.CreatedBy) as 'Section',(select top 1 SecretariatID from [gmc].[tblEMPMappingSecretariat] m where m.EMPID=a.CreatedBy) as 'SecretariatID' from [gmc].[tbl_RCTesting] a
GO

CREATE OR ALTER VIEW [gmc].[VW_GetTTDFRSDtls]
AS
   select  ID,Name,Mobile,
   Image,  
   Template,
   Status,Aadhar, Place ,convert(varchar(10),SlatDate,103) as SlatDate,SlatTime from [gmc].[TblTTDFRSCapture1]
GO
