-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Indexes

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BeneficiaryAttendance' AND object_id = OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]'))
CREATE UNIQUE NONCLUSTERED INDEX [UX_BeneficiaryAttendance] ON [gmc].[tbl_BeneficiaryAttendance]
(
	[BeneficiaryID] ASC,
	[AttendanceDate] ASC,
	[AttendanceType] ASC
)
WHERE ([Is_Active]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IDX_TTDFRS_1' AND object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture]'))
CREATE NONCLUSTERED INDEX [IDX_TTDFRS_1] ON [gmc].[TblTTDFRSCapture]
(
	[Mobile] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'Idx_TTDFRS_2' AND object_id = OBJECT_ID(N'[gmc].[TblTTDFRSCapture]'))
CREATE NONCLUSTERED INDEX [Idx_TTDFRS_2] ON [gmc].[TblTTDFRSCapture]
(
	[Status] ASC,
	[Aadhar] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
GO
