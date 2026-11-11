-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Foreign keys and check constraints

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'), N'BeneficiaryID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]'))
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance] WITH CHECK ADD CONSTRAINT [FK_BeneficiaryAttendance_Beneficiary] FOREIGN KEY([BeneficiaryID]) REFERENCES [gmc].[tbl_BeneficiaryRegistration] ([BeneficiaryID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'), N'Home_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]'))
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance] WITH CHECK ADD CONSTRAINT [FK_BeneficiaryAttendance_Home] FOREIGN KEY([Home_ID]) REFERENCES [gmc].[tbl_Homes] ([Home_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]'), N'Home_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]'))
    ALTER TABLE [gmc].[tbl_BeneficiaryRegistration] WITH CHECK ADD CONSTRAINT [FK_tbl_BeneficiaryRegistration_Home] FOREIGN KEY([Home_ID]) REFERENCES [gmc].[tbl_Homes] ([Home_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_ELSR_FilledDtls]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_ELSR_FilledDtls]'), N'ELSRID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_ELSR]'))
    ALTER TABLE [gmc].[tbl_ELSR_FilledDtls] WITH CHECK ADD CONSTRAINT [FK_ELSR_ID] FOREIGN KEY([ELSRID]) REFERENCES [gmc].[tbl_ELSR] ([ELSRID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_Homes]'), N'HomeCategory_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_HomeCategory]'))
    ALTER TABLE [gmc].[tbl_Homes] WITH CHECK ADD CONSTRAINT [FK_tbl_Homes_HomeCategory] FOREIGN KEY([HomeCategory_ID]) REFERENCES [gmc].[tbl_HomeCategory] ([HomeCategory_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_Homes]'), N'WelfareHomeType_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]'))
    ALTER TABLE [gmc].[tbl_Homes] WITH CHECK ADD CONSTRAINT [FK_tbl_Homes_WelfareHomeType] FOREIGN KEY([WelfareHomeType_ID]) REFERENCES [gmc].[tbl_WelfareHomeTypes] ([WelfareHomeType_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_OTP]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_OTP]'), N'Mobile', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblAppUser]'))
    ALTER TABLE [gmc].[tbl_OTP] WITH CHECK ADD CONSTRAINT [FK_tbl_OTP_Mobile_TblAppUser] FOREIGN KEY([Mobile]) REFERENCES [gmc].[TblAppUser] ([UserName]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[Tbl_Secretariat]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[Tbl_Secretariat]'), N'WardID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblWards]'))
    ALTER TABLE [gmc].[Tbl_Secretariat] WITH CHECK ADD CONSTRAINT [FK_Tbl_Secretariat_WardID_TblWards] FOREIGN KEY([WardID]) REFERENCES [gmc].[TblWards] ([WardID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_users]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_users]'), N'UserType_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tbl_UserTypes]'))
    ALTER TABLE [gmc].[tbl_users] WITH CHECK ADD CONSTRAINT [FK_tbl_users_UserType] FOREIGN KEY([UserType_ID]) REFERENCES [gmc].[tbl_UserTypes] ([UserType_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]'), N'EMPID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblEMPUser]'))
    ALTER TABLE [gmc].[tblEMPMappingSecretariat] WITH CHECK ADD CONSTRAINT [FK_tblEMPMappingSecretariat_EMPID_TblEMPUser] FOREIGN KEY([EMPID]) REFERENCES [gmc].[TblEMPUser] ([EMPID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]'), N'SecretariatID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[Tbl_Secretariat]'))
    ALTER TABLE [gmc].[tblEMPMappingSecretariat] WITH CHECK ADD CONSTRAINT [FK_tblEMPMappingSecretariat_SecretariatID_Tbl_Secretariat] FOREIGN KEY([SecretariatID]) REFERENCES [gmc].[Tbl_Secretariat] ([SecretariatID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblMainGrievance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblMainGrievance]'), N'SectionID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblSections]'))
    ALTER TABLE [gmc].[TblMainGrievance] WITH CHECK ADD CONSTRAINT [FK_TblMainGrievance_SectionID_TblSections] FOREIGN KEY([SectionID]) REFERENCES [gmc].[TblSections] ([SectionID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblSubGrievance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblSubGrievance]'), N'MainGrievanceID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblMainGrievance]'))
    ALTER TABLE [gmc].[TblSubGrievance] WITH CHECK ADD CONSTRAINT [FK_SubGrievance] FOREIGN KEY([MainGrievanceID]) REFERENCES [gmc].[TblMainGrievance] ([MainGrievanceID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblUserGrievance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblUserGrievance]'), N'AddressID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[tblAddress]'))
    ALTER TABLE [gmc].[TblUserGrievance] WITH CHECK ADD CONSTRAINT [FK_TblUserGrievance_AddressID_tblAddress] FOREIGN KEY([AddressID]) REFERENCES [gmc].[tblAddress] ([AddressID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblUserGrievance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblUserGrievance]'), N'SecretariatID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[Tbl_Secretariat]'))
    ALTER TABLE [gmc].[TblUserGrievance] WITH CHECK ADD CONSTRAINT [FK_TblUserGrievance_SecretariatID_Tbl_Secretariat] FOREIGN KEY([SecretariatID]) REFERENCES [gmc].[Tbl_Secretariat] ([SecretariatID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblUserGrievance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblUserGrievance]'), N'SectionID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[gmc].[TblSections]'))
    ALTER TABLE [gmc].[TblUserGrievance] WITH CHECK ADD CONSTRAINT [FK_TblUserGrievance_SectionID_TblSections] FOREIGN KEY([SectionID]) REFERENCES [gmc].[TblSections] ([SectionID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_EMPDailyAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_EMPDailyAttendance]'), N'EMP_ID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[SERP].[tbl_Employee]'))
    ALTER TABLE [SERP].[tbl_EMPDailyAttendance] WITH CHECK ADD CONSTRAINT [FK_tbl_EMPDailyAttendance_EMP_ID_tbl_Employee] FOREIGN KEY([EMP_ID]) REFERENCES [SERP].[tbl_Employee] ([EMP_ID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_Employee]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_Employee]'), N'EmpTypeID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[SERP].[tbl_EMP_Types]'))
    ALTER TABLE [SERP].[tbl_Employee] WITH CHECK ADD CONSTRAINT [FK_tbl_Employee_EmpTypeID_tbl_EMP_Types] FOREIGN KEY([EmpTypeID]) REFERENCES [SERP].[tbl_EMP_Types] ([EmpTypeID]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns
               WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_Employee]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_Employee]'), N'EmpTypeID', 'ColumnId')
                 AND referenced_object_id = OBJECT_ID(N'[SERP].[tbl_EMP_Types]'))
    ALTER TABLE [SERP].[tbl_Employee] WITH CHECK ADD CONSTRAINT [FK_tbl_Employee_EmpTypeID_tbl_EMP_Types] FOREIGN KEY([EmpTypeID]) REFERENCES [SERP].[tbl_EMP_Types] ([EmpTypeID]);
GO

IF OBJECT_ID(N'[gmc].[CK_BeneficiaryAttendance_Type]', N'C') IS NULL
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance]  WITH CHECK ADD  CONSTRAINT [CK_BeneficiaryAttendance_Type] CHECK  (([AttendanceType]='E' OR [AttendanceType]='M'));
GO
