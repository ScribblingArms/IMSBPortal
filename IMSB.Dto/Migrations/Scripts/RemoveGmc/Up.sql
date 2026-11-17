-- Migration RemoveGmcSchemaObjects: drops everything AddGmcDbObjects put in the gmc schema
-- (61 procedures, 2 views, 43 tables with their data, the BeneficiaryAttendanceTVP type, then the schema itself).
-- The SERP, FIN and FW objects are kept; none of them refer to gmc.
-- Batches are separated by GO; each one is safe to re-run.

DROP PROCEDURE IF EXISTS [gmc].[USPVerifyOTP];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPuserReg];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPTruckDriverLogin];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveWaterTankerBooing];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveUserGrievance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveUserCnD];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveTTDFRCCapture];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveFRCCapture];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveEventGarbageDtle];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPSaveAddress];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPRegOTP];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetUserGrievance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetUserDtls];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetUserCndBookingDtls];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetTTDFRCCapture];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetOTPs];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetMaster];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetBookWaterTanker];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPGetAddress];
GO

DROP PROCEDURE IF EXISTS [gmc].[USPEMPLogin];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_UserLogin];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_UpdateStatus_TTDFRCCapture];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Update_Beneficiary_FRS];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_TruckDrver_MasterList];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Save_Bulk_BeneficiaryAttendance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Save_BeneficiaryAttendance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Save_Beneficiary];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_WaterBooking_Consolidation];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_RCTesting];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_Grievance_List];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_Grievance_Consolidation];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_GarbageCollection_Consolidation];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_DB_Portal];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_DB_Mobile_Grievance_DataForEmpUser];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Rpt_CnD_Consolidation];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Maters_List];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_UserGrievance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_RCTesting];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_ELRS_FilledDtls];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_CnD_Assignment_UpdateByTruckDriver];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_CnD_Assignment];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_IU_Approve_UserGrievance_By_MidLevel_EMP];
GO

DROP PROCEDURE IF EXISTS [gmc].[usp_GetSubGrievance];
GO

DROP PROCEDURE IF EXISTS [gmc].[usp_GetMainGrievance];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_WelfareHomeDetails];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_MasterList];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_HomeDashboardCounts];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_GET_Grievance_DataForEmpUser];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_EventGarbageDtls];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_BeneficiaryAttendanceToday];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Get_AdminDashboardCounts];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_GarbageCollection_PaymentConfirmedByEmp];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_EMP_Master_List];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Deactivate_Beneficiary];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Data_For_Amaravathi];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_CnD_PaymentConfirmedByEmp];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_CitizenRegByHelpDesk];
GO

DROP PROCEDURE IF EXISTS [gmc].[USP_Address_Add_ByHelpDesk];
GO

DROP PROCEDURE IF EXISTS [gmc].[sp_SaveBillDeskTransaction];
GO

DROP PROCEDURE IF EXISTS [gmc].[sp_CreateWelfareHome];
GO

DROP PROCEDURE IF EXISTS [gmc].[Proc_Portal_Login];
GO

DROP VIEW IF EXISTS [gmc].[vw_RCTesting];
GO

DROP VIEW IF EXISTS [gmc].[VW_GetTTDFRSDtls];
GO

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
             + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
FROM sys.foreign_keys fk
WHERE fk.parent_object_id IN (
    OBJECT_ID(N'[gmc].[TblEMPUser]'),
    OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]'),
    OBJECT_ID(N'[gmc].[tbl_RCTesting]'),
    OBJECT_ID(N'[gmc].[TblTTDFRSCapture1]'),
    OBJECT_ID(N'[gmc].[ReceivingMode]'),
    OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'),
    OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]'),
    OBJECT_ID(N'[gmc].[tbl_BillDeskTransactions]'),
    OBJECT_ID(N'[gmc].[tbl_ELSR]'),
    OBJECT_ID(N'[gmc].[tbl_ELSR_FilledDtls]'),
    OBJECT_ID(N'[gmc].[tbl_ELSR_TimeSlats]'),
    OBJECT_ID(N'[gmc].[tbl_EventGarbageCollection]'),
    OBJECT_ID(N'[gmc].[tbl_EventParticipationMembers]'),
    OBJECT_ID(N'[gmc].[tbl_EventTimes]'),
    OBJECT_ID(N'[gmc].[tbl_EventTypes]'),
    OBJECT_ID(N'[gmc].[tbl_GarbageCollectionTypes]'),
    OBJECT_ID(N'[gmc].[tbl_GrievanceReceivingCenter]'),
    OBJECT_ID(N'[gmc].[tbl_GrievanceRecievingModes]'),
    OBJECT_ID(N'[gmc].[tbl_HomeCategory]'),
    OBJECT_ID(N'[gmc].[tbl_Homes]'),
    OBJECT_ID(N'[gmc].[tbl_OTP]'),
    OBJECT_ID(N'[gmc].[Tbl_Secretariat]'),
    OBJECT_ID(N'[gmc].[tbl_users]'),
    OBJECT_ID(N'[gmc].[tbl_UserTypes]'),
    OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]'),
    OBJECT_ID(N'[gmc].[tblAddress]'),
    OBJECT_ID(N'[gmc].[TblAppUser]'),
    OBJECT_ID(N'[gmc].[TblCnDAssignments]'),
    OBJECT_ID(N'[gmc].[tblCnDBookingDtls]'),
    OBJECT_ID(N'[gmc].[TblCnDTruckDriverDtls]'),
    OBJECT_ID(N'[gmc].[TblFRSCapture]'),
    OBJECT_ID(N'[gmc].[TblMainGrievance]'),
    OBJECT_ID(N'[gmc].[TblSecretariat]'),
    OBJECT_ID(N'[gmc].[TblSections]'),
    OBJECT_ID(N'[gmc].[TblSubGrievance]'),
    OBJECT_ID(N'[gmc].[TblTTDFRSCapture]'),
    OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]'),
    OBJECT_ID(N'[gmc].[TblUserGrievance]'),
    OBJECT_ID(N'[gmc].[TblUserType]'),
    OBJECT_ID(N'[gmc].[TblWards]'),
    OBJECT_ID(N'[gmc].[TblWasteCategory]'),
    OBJECT_ID(N'[gmc].[TblWasteType]'),
    OBJECT_ID(N'[gmc].[tblWaterTankerBookingDtls]'));
EXEC sp_executesql @sql;
GO

DROP TABLE IF EXISTS [gmc].[TblEMPUser];
GO

DROP TABLE IF EXISTS [gmc].[tblEMPMappingSecretariat];
GO

DROP TABLE IF EXISTS [gmc].[tbl_RCTesting];
GO

DROP TABLE IF EXISTS [gmc].[TblTTDFRSCapture1];
GO

DROP TABLE IF EXISTS [gmc].[ReceivingMode];
GO

DROP TABLE IF EXISTS [gmc].[tbl_BeneficiaryAttendance];
GO

DROP TABLE IF EXISTS [gmc].[tbl_BeneficiaryRegistration];
GO

DROP TABLE IF EXISTS [gmc].[tbl_BillDeskTransactions];
GO

DROP TABLE IF EXISTS [gmc].[tbl_ELSR];
GO

DROP TABLE IF EXISTS [gmc].[tbl_ELSR_FilledDtls];
GO

DROP TABLE IF EXISTS [gmc].[tbl_ELSR_TimeSlats];
GO

DROP TABLE IF EXISTS [gmc].[tbl_EventGarbageCollection];
GO

DROP TABLE IF EXISTS [gmc].[tbl_EventParticipationMembers];
GO

DROP TABLE IF EXISTS [gmc].[tbl_EventTimes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_EventTypes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_GarbageCollectionTypes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_GrievanceReceivingCenter];
GO

DROP TABLE IF EXISTS [gmc].[tbl_GrievanceRecievingModes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_HomeCategory];
GO

DROP TABLE IF EXISTS [gmc].[tbl_Homes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_OTP];
GO

DROP TABLE IF EXISTS [gmc].[Tbl_Secretariat];
GO

DROP TABLE IF EXISTS [gmc].[tbl_users];
GO

DROP TABLE IF EXISTS [gmc].[tbl_UserTypes];
GO

DROP TABLE IF EXISTS [gmc].[tbl_WelfareHomeTypes];
GO

DROP TABLE IF EXISTS [gmc].[tblAddress];
GO

DROP TABLE IF EXISTS [gmc].[TblAppUser];
GO

DROP TABLE IF EXISTS [gmc].[TblCnDAssignments];
GO

DROP TABLE IF EXISTS [gmc].[tblCnDBookingDtls];
GO

DROP TABLE IF EXISTS [gmc].[TblCnDTruckDriverDtls];
GO

DROP TABLE IF EXISTS [gmc].[TblFRSCapture];
GO

DROP TABLE IF EXISTS [gmc].[TblMainGrievance];
GO

DROP TABLE IF EXISTS [gmc].[TblSecretariat];
GO

DROP TABLE IF EXISTS [gmc].[TblSections];
GO

DROP TABLE IF EXISTS [gmc].[TblSubGrievance];
GO

DROP TABLE IF EXISTS [gmc].[TblTTDFRSCapture];
GO

DROP TABLE IF EXISTS [gmc].[TblTTDFRSCapture_New];
GO

DROP TABLE IF EXISTS [gmc].[TblUserGrievance];
GO

DROP TABLE IF EXISTS [gmc].[TblUserType];
GO

DROP TABLE IF EXISTS [gmc].[TblWards];
GO

DROP TABLE IF EXISTS [gmc].[TblWasteCategory];
GO

DROP TABLE IF EXISTS [gmc].[TblWasteType];
GO

DROP TABLE IF EXISTS [gmc].[tblWaterTankerBookingDtls];
GO

DROP TYPE IF EXISTS [gmc].[BeneficiaryAttendanceTVP];
GO

IF SCHEMA_ID(N'gmc') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'gmc')) AND NOT EXISTS (SELECT 1 FROM sys.types WHERE schema_id = SCHEMA_ID(N'gmc'))
    EXEC(N'DROP SCHEMA [gmc]');
GO
