-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Tables (gmc, FIN, FW, SERP)

IF OBJECT_ID(N'[gmc].[TblEMPUser]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblEMPUser](
	[EMPID] [int] IDENTITY(10000,1) NOT NULL,
	[UserName] [varchar](10) NOT NULL,
	[Password] [nvarchar](500) NULL,
	[FullName] [nvarchar](200) NULL,
	[Mobile] [varchar](10) NOT NULL,
	[Section] [int] NULL,
	[UserType] [int] NULL,
	[IsActive] [tinyint] NOT NULL,
	[ModifiedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[AttemptOn] [datetime] NULL,
	[Role] [varchar](50) NULL,
	[Logincount] [int] NULL,
	[LoginAttempTime] [datetime] NULL,
	[Lastlogin] [date] NULL,
 CONSTRAINT [PK_TblEMPUser] PRIMARY KEY CLUSTERED 
(
	[EMPID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[UserName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tblEMPMappingSecretariat]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tblEMPMappingSecretariat](
	[EMPID] [int] NOT NULL,
	[SecretariatID] [int] NOT NULL,
	[Role] [varchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[EMPID] ASC,
	[SecretariatID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_RCTesting]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_RCTesting](
	[RCTestingID] [int] IDENTITY(1,1) NOT NULL,
	[ELSRID] [int] NULL,
	[ImageBase64] [nvarchar](max) NULL,
	[Description] [nvarchar](500) NULL,
	[CreatedBy] [int] NULL,
	[CreateOn] [datetime] NULL,
	[Result] [varchar](300) NULL,
PRIMARY KEY CLUSTERED 
(
	[RCTestingID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblTTDFRSCapture1]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblTTDFRSCapture1](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](500) NULL,
	[Mobile] [varchar](10) NULL,
	[Image] [varchar](max) NULL,
	[Template] [varchar](max) NULL,
	[Status] [tinyint] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[Aadhar] [varchar](12) NULL,
	[Place] [varchar](100) NULL,
	[SlatDate] [datetime] NULL,
	[SlatTime] [varchar](20) NULL,
	[ImageTxt] [text] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[ReceivingMode]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[ReceivingMode](
	[ReceivingModeID] [int] IDENTITY(1,1) NOT NULL,
	[ReceivingMode] [varchar](100) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_BeneficiaryAttendance]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_BeneficiaryAttendance](
	[AttendanceID] [bigint] IDENTITY(1,1) NOT NULL,
	[BeneficiaryID] [int] NOT NULL,
	[Home_ID] [int] NOT NULL,
	[AttendanceDate] [date] NOT NULL,
	[AttendanceType] [char](1) NOT NULL,
	[AttendanceTime] [datetime] NOT NULL,
	[Latitude] [decimal](10, 7) NULL,
	[Longitude] [decimal](10, 7) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[Is_Active] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_BeneficiaryRegistration]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_BeneficiaryRegistration](
	[BeneficiaryID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](250) NOT NULL,
	[Gender] [nvarchar](20) NULL,
	[DOB] [date] NULL,
	[Age] [int] NULL,
	[FatherMotherGuardianName] [nvarchar](250) NULL,
	[MaritalStatus] [nvarchar](50) NULL,
	[MobileNumber] [nvarchar](20) NULL,
	[Aadhaar] [nvarchar](20) NULL,
	[ImagePath] [nvarchar](500) NULL,
	[ImageFileName] [nvarchar](250) NULL,
	[ImageTemplate] [nvarchar](max) NULL,
	[FRSStatus] [tinyint] NULL,
	[Home_ID] [int] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[Is_Active] [bit] NOT NULL,
	[FRS_UpdatedOn] [datetime] NULL,
	[DeactivatedRemarks] [varchar](100) NULL,
	[DeactivatedOn] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[BeneficiaryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_BillDeskTransactions]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_BillDeskTransactions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OrderId] [nvarchar](50) NULL,
	[TransactionId] [nvarchar](50) NULL,
	[Status] [nvarchar](20) NULL,
	[AuthStatus] [nvarchar](10) NULL,
	[BankRefNo] [nvarchar](50) NULL,
	[Amount] [decimal](18, 2) NULL,
	[PaymentMethod] [nvarchar](20) NULL,
	[TransactionDate] [datetime] NULL,
	[CreatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_ELSR]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_ELSR](
	[ELSRID] [int] IDENTITY(1,1) NOT NULL,
	[ELSR_Location] [varchar](100) NULL,
	[ELSR_Capacity] [varchar](30) NULL,
	[AE_Code] [varchar](50) NULL,
	[Source] [varchar](100) NULL,
	[AE_ID] [int] NULL,
	[Capacity] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[ELSRID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_ELSR_FilledDtls]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_ELSR_FilledDtls](
	[ELSRFilledID] [int] IDENTITY(1,1) NOT NULL,
	[ELSRID] [int] NULL,
	[FilledDate] [datetime] NULL,
	[ELSRTimeSlatID] [int] NULL,
	[Noof_KL_Filled] [varchar](20) NULL,
	[Remaks] [varchar](100) NULL,
	[AE_ID] [int] NULL,
	[CreatedOn] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[ELSRFilledID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UniqueELSRIDDateTimeSlat] UNIQUE NONCLUSTERED 
(
	[ELSRID] ASC,
	[FilledDate] ASC,
	[ELSRTimeSlatID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_ELSR_TimeSlats]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_ELSR_TimeSlats](
	[ELSRTimeSlatID] [int] NULL,
	[TiemSlat] [varchar](20) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_EventGarbageCollection]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_EventGarbageCollection](
	[EGCollectionID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NOT NULL,
	[EventTypeID] [int] NULL,
	[EventTimeID] [int] NULL,
	[EventnoofMemberId] [int] NULL,
	[NoofTrips] [int] NULL,
	[TotalPrice] [decimal](18, 2) NULL,
	[BookingDate] [datetime] NULL,
	[EGCollectionStatus] [int] NULL,
	[AddressID] [int] NULL,
	[Langitude] [varchar](50) NULL,
	[Latitude] [varchar](50) NULL,
	[UpdatedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[PaymentConfirmedBy] [int] NULL,
	[PaymentConfirmedOn] [datetime] NULL,
	[ClosedDate] [datetime] NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_EventParticipationMembers]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_EventParticipationMembers](
	[EventnoofMemberId] [int] IDENTITY(1,1) NOT NULL,
	[NoofMembers] [varchar](50) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_EventTimes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_EventTimes](
	[EventTimeID] [int] IDENTITY(1,1) NOT NULL,
	[EventTime] [varchar](50) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_EventTypes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_EventTypes](
	[EventTypeID] [int] IDENTITY(1,1) NOT NULL,
	[EventType] [varchar](50) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_GarbageCollectionTypes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_GarbageCollectionTypes](
	[GarbageCollectionTypeID] [int] IDENTITY(1,1) NOT NULL,
	[GarbageCollectionTypeName] [varchar](200) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_GrievanceReceivingCenter]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_GrievanceReceivingCenter](
	[CenterID] [int] IDENTITY(1,1) NOT NULL,
	[CenterName] [varchar](100) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_GrievanceRecievingModes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_GrievanceRecievingModes](
	[ReceivingModeID] [int] IDENTITY(1,1) NOT NULL,
	[ReceivingMode] [varchar](20) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_HomeCategory]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_HomeCategory](
	[HomeCategory_ID] [int] IDENTITY(1,1) NOT NULL,
	[HomeCategory_Name] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[HomeCategory_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_Homes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_Homes](
	[Home_ID] [int] IDENTITY(1,1) NOT NULL,
	[Home_Name] [nvarchar](250) NOT NULL,
	[WelfareHomeType_ID] [int] NOT NULL,
	[HomeCategory_ID] [int] NOT NULL,
	[Registration_Number] [nvarchar](100) NOT NULL,
	[Registration_Date] [date] NULL,
	[Establishment_Date] [date] NULL,
	[Current_Status] [nvarchar](50) NOT NULL,
	[Managing_Organization] [nvarchar](250) NULL,
	[Year_Established] [int] NULL,
	[Home_Capacity] [int] NULL,
	[Current_Occupancy] [int] NULL,
	[CreatedOn] [datetime] NOT NULL,
	[ModifiedOn] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Home_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_tbl_Homes_Registration_Number] UNIQUE NONCLUSTERED 
(
	[Registration_Number] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_OTP]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_OTP](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[Mobile] [varchar](10) NOT NULL,
	[OTP] [int] NOT NULL,
	[OTPStatus] [tinyint] NOT NULL,
	[CreatedDate] [datetime] NULL,
	[ExpiryDate] [datetime] NULL,
	[ModifiedDate] [datetime] NULL,
	[TmpToken] [varchar](100) NOT NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[Tbl_Secretariat]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[Tbl_Secretariat](
	[SecretariatID] [int] NOT NULL,
	[SecretariatName] [nvarchar](100) NULL,
	[WardID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SecretariatID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_users]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_users](
	[User_ID] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](200) NOT NULL,
	[Username] [nvarchar](100) NOT NULL,
	[Password] [nvarchar](500) NOT NULL,
	[UserType_ID] [int] NOT NULL,
	[Map_Home_ID] [int] NULL,
	[CreatedDate] [datetime] NOT NULL,
	[Is_Active] [bit] NOT NULL,
	[LastLogin] [datetime] NULL,
	[Failed_Attempts] [int] NOT NULL,
	[Last_Failed_Attempt] [datetime] NULL,
	[Lockout_Until] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[User_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_tbl_users_Username] UNIQUE NONCLUSTERED 
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_UserTypes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_UserTypes](
	[UserType_ID] [int] IDENTITY(1,1) NOT NULL,
	[UserType_Name] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[UserType_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tbl_WelfareHomeTypes]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tbl_WelfareHomeTypes](
	[WelfareHomeType_ID] [int] IDENTITY(1,1) NOT NULL,
	[WelfareHomeType_Name] [nvarchar](200) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[WelfareHomeType_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tblAddress]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tblAddress](
	[AddressID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NULL,
	[HNo] [nvarchar](100) NULL,
	[Building_Name] [nvarchar](100) NULL,
	[StreetOrLineNum] [nvarchar](50) NULL,
	[Area_Name] [nvarchar](200) NULL,
	[LandMark] [nvarchar](200) NULL,
	[City] [nvarchar](100) NULL,
	[nickname] [nvarchar](100) NULL,
	[ModifiedBy] [int] NULL,
	[Langitude] [varchar](50) NULL,
	[Latitude] [varchar](50) NULL,
	[AddressStatus] [tinyint] NULL,
	[pincode] [varchar](6) NULL,
	[WardID] [int] NULL,
	[SecretariatID] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[mobile] [varchar](10) NULL,
PRIMARY KEY CLUSTERED 
(
	[AddressID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblAppUser]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblAppUser](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[UserName] [varchar](10) NOT NULL,
	[Password] [nvarchar](500) NULL,
	[FullName] [nvarchar](200) NULL,
	[Family_Member_count] [int] NULL,
	[HNo] [nvarchar](100) NULL,
	[Building_Name] [nvarchar](100) NULL,
	[StreetOrLineNum] [nvarchar](50) NULL,
	[Area_Name] [nvarchar](200) NULL,
	[LandMark] [nvarchar](200) NULL,
	[City] [nvarchar](100) NULL,
	[Pincode] [int] NULL,
	[Mobile] [varchar](10) NOT NULL,
	[IsActive] [tinyint] NOT NULL,
	[ModifiedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[AttemptOn] [datetime] NULL,
	[UID] [nvarchar](11) NULL,
	[WardID] [int] NULL,
	[SecretariatID] [int] NULL,
	[CreatedBy] [int] NULL,
 CONSTRAINT [PK_AppUser] PRIMARY KEY CLUSTERED 
(
	[UserName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Mobile] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblCnDAssignments]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblCnDAssignments](
	[CnDAssignmentID] [int] IDENTITY(1,1) NOT NULL,
	[CnDBookingID] [int] NOT NULL,
	[EMPID] [int] NOT NULL,
	[TruckDriverID] [int] NOT NULL,
	[AssignmentDate] [datetime] NULL,
	[CompletionDate] [datetime] NULL,
	[CnDStatus] [nvarchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[CnDAssignmentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tblCnDBookingDtls]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tblCnDBookingDtls](
	[CnDBookingID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NOT NULL,
	[WasteCategoryID] [int] NULL,
	[WasteTypeID] [int] NULL,
	[QtyInTons] [int] NULL,
	[TotalPrice] [decimal](18, 2) NULL,
	[BookingDate] [datetime] NULL,
	[BookingTime] [nvarchar](10) NULL,
	[CnDBookingStatus] [int] NULL,
	[AddressID] [int] NULL,
	[BookingFor] [varchar](50) NOT NULL,
	[Langitude] [varchar](50) NULL,
	[Latitude] [varchar](50) NULL,
	[UpdatedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[PaymentConfirmedBy] [int] NULL,
	[PaymentConfirmedOn] [datetime] NULL,
	[ClosedDate] [datetime] NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblCnDTruckDriverDtls]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblCnDTruckDriverDtls](
	[TruckDriverID] [int] IDENTITY(1,1) NOT NULL,
	[UserName] [varchar](10) NULL,
	[Password] [nvarchar](500) NULL,
	[DriverName] [nvarchar](200) NULL,
	[DriverMobile] [varchar](10) NOT NULL,
	[TruckNum] [varchar](20) NOT NULL,
	[IsActive] [tinyint] NOT NULL,
	[AvailabilityStatus] [tinyint] NOT NULL,
	[ModifiedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[AttemptOn] [datetime] NULL,
UNIQUE NONCLUSTERED 
(
	[DriverMobile] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblFRSCapture]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblFRSCapture](
	[FID] [int] IDENTITY(1,1) NOT NULL,
	[Aadhar] [nvarchar](12) NULL,
	[ImageBase64] [varchar](max) NULL,
	[IsActive] [tinyint] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[Image1] [nvarchar](200) NULL,
PRIMARY KEY CLUSTERED 
(
	[FID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblMainGrievance]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblMainGrievance](
	[MainGrievanceID] [int] IDENTITY(1,1) NOT NULL,
	[MainGrievanceName] [nvarchar](100) NULL,
	[SectionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[MainGrievanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblSecretariat]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblSecretariat](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[WardID] [int] NULL,
	[SecretariatID] [int] NULL,
	[SecretariatName] [nvarchar](100) NULL,
	[SecretaryName] [nvarchar](100) NULL,
	[ContactNum] [varchar](10) NULL,
	[IsIncharge] [tinyint] NULL,
	[AreaName] [varchar](300) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblSections]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblSections](
	[SectionID] [int] IDENTITY(1,1) NOT NULL,
	[SectionName] [varchar](100) NULL,
 CONSTRAINT [PK_Section] PRIMARY KEY CLUSTERED 
(
	[SectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblSubGrievance]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblSubGrievance](
	[SubGrievanceID] [int] IDENTITY(1,1) NOT NULL,
	[SubGrievanceName] [nvarchar](100) NULL,
	[MainGrievanceID] [int] NULL,
	[DueHours] [int] NULL,
	[SectionID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[SubGrievanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblTTDFRSCapture]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblTTDFRSCapture](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](500) NULL,
	[Mobile] [varchar](10) NULL,
	[Image] [varchar](max) NULL,
	[Template] [varchar](max) NULL,
	[Status] [tinyint] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[Aadhar] [varchar](12) NULL,
	[Place] [varchar](100) NULL,
	[SlatDate] [datetime] NULL,
	[SlatTime] [varchar](20) NULL,
	[ImageTxt] [text] NULL,
PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblTTDFRSCapture_New]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblTTDFRSCapture_New](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](500) NULL,
	[Mobile] [varchar](10) NULL,
	[Image] [text] NULL,
	[Template] [varchar](max) NULL,
	[Status] [tinyint] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[Aadhar] [varchar](12) NULL,
	[Place] [varchar](100) NULL,
	[SlatDate] [datetime] NULL,
	[SlatTime] [varchar](20) NULL,
PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblUserGrievance]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblUserGrievance](
	[UserGrievanceID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NULL,
	[Username] [varchar](10) NULL,
	[MainGrievanceID] [int] NULL,
	[SubGrievanceID] [int] NULL,
	[Description] [nvarchar](500) NULL,
	[ImageBase64] [nvarchar](max) NULL,
	[Image1] [nvarchar](200) NULL,
	[GrievanceStatus] [int] NULL,
	[ModifiedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[Langitude] [varchar](50) NULL,
	[Latitude] [varchar](50) NULL,
	[UpdatedByEMPID] [int] NULL,
	[EMPGrievanceStatus] [int] NULL,
	[EMPImageBase64] [nvarchar](max) NULL,
	[EMPUpdatedDate] [datetime] NULL,
	[AddressID] [int] NULL,
	[OtherGrievanceText] [varchar](200) NULL,
	[SectionID] [int] NULL,
	[SecretariatID] [int] NULL,
	[ApprovedStatus] [int] NULL,
	[ApprovedBy] [int] NULL,
	[ApprovedOn] [datetime] NULL,
	[EMPDescription] [varchar](500) NULL,
	[ReceivingModeID] [int] NULL,
	[CenterID] [int] NULL,
	[ReceivingCenter_Details] [varchar](500) NULL,
	[Email] [varchar](50) NULL,
	[CreateDByHD] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[UserGrievanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblUserType]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblUserType](
	[UserTypeID] [int] IDENTITY(1,1) NOT NULL,
	[UserTypeName] [varchar](50) NOT NULL,
	[SectionID] [int] NULL,
	[SectionName] [varchar](100) NULL,
	[CreatedBy] [int] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[Role] [varchar](30) NULL,
 CONSTRAINT [PK_TblUserType] PRIMARY KEY CLUSTERED 
(
	[UserTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblWards]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblWards](
	[WardID] [int] NOT NULL,
	[WardName] [varchar](20) NULL,
PRIMARY KEY CLUSTERED 
(
	[WardID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblWasteCategory]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblWasteCategory](
	[WasteCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[WasteCategoryName] [varchar](100) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[TblWasteType]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[TblWasteType](
	[WasteTypeID] [int] IDENTITY(1,1) NOT NULL,
	[WasteTypeName] [varchar](50) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[gmc].[tblWaterTankerBookingDtls]', N'U') IS NULL
BEGIN
CREATE TABLE [gmc].[tblWaterTankerBookingDtls](
	[WaterTankerID] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NOT NULL,
	[BookingType] [int] NOT NULL,
	[OneThousendLtrs] [int] NOT NULL,
	[FiveThousendLtrs] [int] NOT NULL,
	[TenThousendLtrs] [int] NOT NULL,
	[TotalLtrs] [int] NOT NULL,
	[TotalAmount] [decimal](18, 2) NOT NULL,
	[TankerStatus] [int] NULL,
	[AddressID] [int] NULL,
	[Langitude] [varchar](50) NULL,
	[Latitude] [varchar](50) NULL,
	[UpdatedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[BookingDate] [datetime] NULL,
	[BookingTime] [nvarchar](10) NULL,
	[ClosedDate] [datetime] NULL,
	[ClosedTime] [nvarchar](10) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[FIN].[tblAmountGiven]', N'U') IS NULL
BEGIN
CREATE TABLE [FIN].[tblAmountGiven](
	[GivenID] [int] IDENTITY(1,1) NOT NULL,
	[CustID] [int] NULL,
	[GivenAmount] [money] NULL,
	[NoofWeeks] [int] NULL,
	[Status] [tinyint] NULL,
	[CreateDate] [datetime] NULL,
	[ModifiedDate] [datetime] NULL,
	[DOG] [varchar](10) NULL,
	[PaymentType] [int] NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[FIN].[tblAmountRecieved]', N'U') IS NULL
BEGIN
CREATE TABLE [FIN].[tblAmountRecieved](
	[RecievedID] [int] IDENTITY(1,1) NOT NULL,
	[GivenID] [int] NULL,
	[CustID] [int] NULL,
	[RecievedAmount] [money] NULL,
	[DOR] [varchar](10) NULL,
	[CreateDate] [datetime] NULL,
	[ModifiedDate] [datetime] NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[FIN].[tblFinCustomers]', N'U') IS NULL
BEGIN
CREATE TABLE [FIN].[tblFinCustomers](
	[CustID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Mobile] [varchar](10) NOT NULL,
	[CreateDate] [datetime] NULL,
	[ModifiedDate] [datetime] NULL,
	[IsActive] [tinyint] NULL,
UNIQUE NONCLUSTERED 
(
	[Mobile] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[FW].[TblLog]', N'U') IS NULL
BEGIN
CREATE TABLE [FW].[TblLog](
	[LogID] [int] IDENTITY(1,1) NOT NULL,
	[PortalUserID] [int] NULL,
	[AppUserID] [int] NULL,
	[AppType] [varchar](50) NULL,
	[Date] [datetime] NOT NULL,
	[Level] [varchar](10) NOT NULL,
	[Message] [varchar](1000) NOT NULL,
	[LogIP] [varchar](20) NULL,
	[Context] [varchar](max) NOT NULL,
 CONSTRAINT [PK_Log] PRIMARY KEY CLUSTERED 
(
	[LogID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_ArtisansTypeAgency]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_ArtisansTypeAgency](
	[AgencyID] [int] NULL,
	[Agency] [varchar](50) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_Designation]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_Designation](
	[DesigID] [int] NULL,
	[Designation] [varchar](50) NULL,
	[EmpTypeID] [int] NULL,
	[BaseSalary] [decimal](10, 2) NULL,
	[ServicePct] [decimal](5, 2) NULL,
	[GSTPercent] [decimal](5, 2) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_EMP_Types]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_EMP_Types](
	[EmpTypeID] [int] NOT NULL,
	[EmpType] [varchar](50) NULL,
PRIMARY KEY CLUSTERED 
(
	[EmpTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_EmpAttendance_Holiday]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_EmpAttendance_Holiday](
	[AttID] [int] IDENTITY(1,1) NOT NULL,
	[EmpID] [int] NOT NULL,
	[AttendanceDate] [date] NOT NULL,
	[Status] [char](1) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AttID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_EmployeeAttendance_EmpDate] UNIQUE NONCLUSTERED 
(
	[EmpID] ASC,
	[AttendanceDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_EMPDailyAttendance]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_EMPDailyAttendance](
	[AttendanceID] [int] IDENTITY(1,1) NOT NULL,
	[EMP_ID] [int] NULL,
	[DOA] [datetime] NULL,
	[EntryTime] [datetime] NULL,
	[ExitTime] [datetime] NULL,
	[CreateOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[ShiftID] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_Employee]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_Employee](
	[EMP_ID] [int] IDENTITY(1,1) NOT NULL,
	[EMP_Name] [varchar](100) NULL,
	[Aadhar] [varchar](12) NULL,
	[Mobile] [varchar](10) NULL,
	[EmpTypeID] [int] NULL,
	[Desig] [int] NULL,
	[ShiftID] [int] NULL,
	[ImageBase64] [nvarchar](max) NULL,
	[Template] [varchar](max) NULL,
	[IsActive] [int] NULL,
	[CreateOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[ImagePath] [varchar](200) NULL,
	[Gender] [varchar](10) NULL,
	[StallNum] [int] NULL,
	[AgencyID] [int] NULL,
	[Remarks] [varchar](200) NULL,
PRIMARY KEY CLUSTERED 
(
	[EMP_ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_Holiday]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_Holiday](
	[HolidayID] [int] IDENTITY(1,1) NOT NULL,
	[HolidayDate] [date] NOT NULL,
	[HolidayName] [varchar](200) NULL,
	[IsSunday] [bit] NOT NULL,
	[IsGovtHoliday] [bit] NOT NULL,
	[CreatedOn] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[HolidayID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[HolidayDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_Section]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_Section](
	[SectionID] [int] NULL,
	[Section] [varchar](100) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[tbl_Shifts]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[tbl_Shifts](
	[ShiftID] [int] NULL,
	[ShiftTime] [varchar](50) NULL,
	[EmpTypeID] [int] NULL,
	[InTime] [time](7) NULL,
	[Outtime] [time](7) NULL
) ON [PRIMARY];
END
GO

IF OBJECT_ID(N'[SERP].[TblUser]', N'U') IS NULL
BEGIN
CREATE TABLE [SERP].[TblUser](
	[UserID] [int] IDENTITY(10000,1) NOT NULL,
	[UserName] [varchar](10) NOT NULL,
	[Password] [nvarchar](500) NULL,
	[FullName] [nvarchar](200) NULL,
	[Mobile] [varchar](10) NOT NULL,
	[Section] [int] NULL,
	[UserType] [int] NULL,
	[IsActive] [tinyint] NOT NULL,
	[ModifiedBy] [int] NULL,
	[CreatedOn] [datetime] NULL,
	[ModifiedOn] [datetime] NULL,
	[AttemptOn] [datetime] NULL,
	[Role] [varchar](50) NULL,
	[Logincount] [int] NULL,
	[LoginAttempTime] [datetime] NULL,
	[Lastlogin] [date] NULL,
 CONSTRAINT [PK_TblEMPUser] PRIMARY KEY CLUSTERED 
(
	[UserID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[UserName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY];
END
GO
