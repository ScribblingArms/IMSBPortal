-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Column defaults

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'), N'AttendanceTime', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance] ADD  DEFAULT (getdate()) FOR [AttendanceTime];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'), N'Is_Active', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_BeneficiaryAttendance] ADD  DEFAULT ((1)) FOR [Is_Active];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_BeneficiaryRegistration] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]'), N'Is_Active', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_BeneficiaryRegistration] ADD  DEFAULT ((1)) FOR [Is_Active];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_HomeCategory]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_HomeCategory]'), N'IsActive', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_HomeCategory] ADD  DEFAULT ((1)) FOR [IsActive];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_HomeCategory]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_HomeCategory]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_HomeCategory] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_Homes]'), N'Current_Status', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_Homes] ADD  DEFAULT ('Active') FOR [Current_Status];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_Homes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_Homes]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_Homes] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_users]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_users]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_users] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_users]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_users]'), N'Is_Active', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_users] ADD  DEFAULT ((1)) FOR [Is_Active];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_users]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_users]'), N'Failed_Attempts', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_users] ADD  DEFAULT ((0)) FOR [Failed_Attempts];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_UserTypes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_UserTypes]'), N'IsActive', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_UserTypes] ADD  DEFAULT ((1)) FOR [IsActive];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_UserTypes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_UserTypes]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_UserTypes] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]'), N'IsActive', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_WelfareHomeTypes] ADD  DEFAULT ((1)) FOR [IsActive];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [gmc].[tbl_WelfareHomeTypes] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblAppUser]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblAppUser]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [gmc].[TblAppUser] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblCnDAssignments]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblCnDAssignments]'), N'AssignmentDate', 'ColumnId'))
    ALTER TABLE [gmc].[TblCnDAssignments] ADD  DEFAULT (getdate()) FOR [AssignmentDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblCnDAssignments]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblCnDAssignments]'), N'CnDStatus', 'ColumnId'))
    ALTER TABLE [gmc].[TblCnDAssignments] ADD  DEFAULT ('Assigned') FOR [CnDStatus];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblFRSCapture]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblFRSCapture]'), N'IsActive', 'ColumnId'))
    ALTER TABLE [gmc].[TblFRSCapture] ADD  DEFAULT ((1)) FOR [IsActive];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblFRSCapture]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblFRSCapture]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [gmc].[TblFRSCapture] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblTTDFRSCapture]'), N'Status', 'ColumnId'))
    ALTER TABLE [gmc].[TblTTDFRSCapture] ADD  DEFAULT ((0)) FOR [Status];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblTTDFRSCapture]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [gmc].[TblTTDFRSCapture] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]'), N'Status', 'ColumnId'))
    ALTER TABLE [gmc].[TblTTDFRSCapture_New] ADD  DEFAULT ((0)) FOR [Status];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [gmc].[TblTTDFRSCapture_New] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_EmpAttendance_Holiday]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_EmpAttendance_Holiday]'), N'CreatedDate', 'ColumnId'))
    ALTER TABLE [SERP].[tbl_EmpAttendance_Holiday] ADD  DEFAULT (getdate()) FOR [CreatedDate];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_Holiday]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_Holiday]'), N'IsSunday', 'ColumnId'))
    ALTER TABLE [SERP].[tbl_Holiday] ADD  DEFAULT ((0)) FOR [IsSunday];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_Holiday]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_Holiday]'), N'IsGovtHoliday', 'ColumnId'))
    ALTER TABLE [SERP].[tbl_Holiday] ADD  DEFAULT ((0)) FOR [IsGovtHoliday];
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'[SERP].[tbl_Holiday]') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[SERP].[tbl_Holiday]'), N'CreatedOn', 'ColumnId'))
    ALTER TABLE [SERP].[tbl_Holiday] ADD  DEFAULT (getdate()) FOR [CreatedOn];
GO
