-- Generated from GMC_sch.sql (gmc_db, scripted 11/11/2026) for migration AddGmcDbObjects.
-- Batches are separated by GO; each one is safe to re-run.
-- Stored procedures (gmc, FIN, SERP)

IF OBJECT_ID(N'[gmc].[Proc_Portal_Login]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[Proc_Portal_Login] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[Proc_Portal_Login]

@UserName varchar(150)=null,
@Password varchar(200)=null

AS


--Declare @UserName varchar(150)='Admi'
--Declare @Password varchar(250)='LZyQVJSdTWk/bSnWA6aGxCUIiFliayhvUkF5q7HUyPM='

DECLARE @ExistUser INT  
DECLARE @SuccessUser INT
DECLARE @Attempts INT
Declare @LoginTime Datetime
Declare @TimeDiff int

  
BEGIN  
 -- SET NOCOUNT ON added to prevent extra result sets from  
 -- interfering with SELECT statements.  
 SET NOCOUNT ON;  

 select @ExistUser= count(*) from [gmc].[TblEMPUser] where UserName=@UserName 
 print @ExistUser
 select @SuccessUser= count(*) from [gmc].[TblEMPUser] where UserName=@UserName and Password=@Password 
 print @SuccessUser
 select @Attempts= isnull(Logincount,0),@LoginTime=isnull(LoginAttempTime,GETDATE()) from [gmc].[TblEMPUser] where UserName=@UserName 
 print @Attempts
 print @LoginTime
 set @TimeDiff=DATEDIFF(MINUTE,@LoginTime, getdate() )
 print @TimeDiff

 if @ExistUser = 1 
 BEGIN
     IF @SuccessUser=0
	 BEGIN
	     IF @Attempts <= 4
		   BEGIN
		         update [gmc].[TblEMPUser] set Logincount=isnull(Logincount,0) + 1,LoginAttempTime=GETDATE() where UserName=@UserName 
				 select '111' StatusCode,'Invalid Password' as StatusMessage,@Attempts 'Logincount'

		   END
		 ELSE
		   BEGIN
		        select '222' StatusCode,'you reached maximum login attempts,Please try after 30 min' as StatusMessage,@Attempts 'Logincount'
		   END
	 END
	 ELSE IF @SuccessUser=1
	 BEGIN
	     IF @Attempts > 4
		 BEGIN
		      IF (@TimeDiff>30)
			  BEGIN
		       update [gmc].[TblEMPUser] set Logincount=0 ,LoginAttempTime=null where UserName=@UserName and Password=@Password 
               set @Attempts=0
			   select '000' StatusCode,'Login Successful.' as StatusMessage,@Attempts as  'Logincount',
			    EMPID,UserName,FullName,Convert(VARCHAR,Lastlogin,103) as 'LastLog',
                UserType,Role
				FROM [gmc].[TblEMPUser] where UserName=@UserName and Password=@Password  
--lost Login count
UPDATE [gmc].[TblEMPUser] SET Lastlogin=GETDATE()where UserName=@UserName and Password=@Password  
			  END
			  ELSE
			  BEGIN
			      select '222' StatusCode,'you reached maximum login attempts,Please try after 30 min' as StatusMessage,@Attempts 'Logincount'
			  END
		 END
		 ELSE
		 BEGIN
		       update [gmc].[TblEMPUser] set Logincount=0 ,LoginAttempTime=null where UserName=@UserName and Password=@Password 
			   select '000' StatusCode,'Login Successful.' as StatusMessage,@Attempts as  'Logincount',
			    EMPID,UserName,FullName,Convert(VARCHAR,Lastlogin,103) as 'LastLog',
                    UserType,Role
FROM [gmc].[TblEMPUser] where UserName=@UserName and Password=@Password  
--lost Login count
UPDATE [gmc].[TblEMPUser] SET Lastlogin=GETDATE()where UserName=@UserName and Password=@Password  
		 END

	 END

 END
 ELSE IF @ExistUser = 0
 BEGIN
     set @Attempts=0
     select '333' as StatusCode,'Invalid Credentials' as StatusMessage,@Attempts 'Logincount'
 END

 --select @ExistUser,@SuccessUser

  
  

END



--select * from [gmc].[TblEMPUser]
--EXEC [Proc_DWO_UserLogin_New] @UserName='dwowcdsc_mah',@Password='GPabvMaTUX0HNAKpuE90BAT7UXfw5ubbrXATyqxlDHA='
--EXEC [Proc_DWO_UserLogin_New] @UserName='cdpoboaadi',@Password='O9tGFu5GoK'
GO

IF OBJECT_ID(N'[gmc].[sp_CreateWelfareHome]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[sp_CreateWelfareHome] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[sp_CreateWelfareHome]
(
    -- Home Details
    @Home_Name NVARCHAR(250),
    @WelfareHomeType_ID INT,
    @HomeCategory_ID INT,
    @Registration_Number NVARCHAR(100),
    @Registration_Date DATE = NULL,
    @Establishment_Date DATE = NULL,
    @Current_Status NVARCHAR(50) = 'Active',
    @Managing_Organization NVARCHAR(250) = NULL,
    @Year_Established INT = NULL,
    @Home_Capacity INT = NULL,
    @Current_Occupancy INT = NULL,

    -- User Details
    --@FullName NVARCHAR(200),
    --@Username NVARCHAR(100),
    @Password NVARCHAR(500)

    -- Output
    
)
AS
BEGIN

    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    declare @SuccessCode varchar(3) 
    declare @SuccessMessage NVARCHAR(500) 

    BEGIN TRY

        BEGIN TRANSACTION;

        /* =========================================
           1. Check Registration Number
           ========================================= */

        IF EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_Homes]
            WHERE Registration_Number = @Registration_Number
        )
        BEGIN

            SET @SuccessCode = '111';
            SET @SuccessMessage =
                'Registration Number already exists.';
            select @SuccessCode as 'Status',@SuccessMessage as 'Message'
            ROLLBACK TRANSACTION;
            RETURN;

        END


        /* =========================================
           2. Check Username
           ========================================= */

        IF EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_users]
            WHERE Username = @Registration_Number
        )
        BEGIN

            SET @SuccessCode = '111';
            SET @SuccessMessage =
                'Username already exists.';
                select @SuccessCode as 'Status',@SuccessMessage as 'Message'
            ROLLBACK TRANSACTION;
            RETURN;

        END


        /* =========================================
           3. Validate Home Type
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_WelfareHomeTypes]
            WHERE WelfareHomeType_ID = @WelfareHomeType_ID
              AND IsActive = 1
        )
        BEGIN

            SET @SuccessCode = '111';
            SET @SuccessMessage =
                'Invalid Welfare Home Type.';
                select @SuccessCode as 'Status',@SuccessMessage as 'Message'
            ROLLBACK TRANSACTION;
            RETURN;

        END


        /* =========================================
           4. Validate Home Category
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_HomeCategory]
            WHERE HomeCategory_ID = @HomeCategory_ID
              AND IsActive = 1
        )
        BEGIN

            SET @SuccessCode = '111';
            SET @SuccessMessage =
                'Invalid Home Category.';
                select @SuccessCode as 'Status',@SuccessMessage as 'Message'
            ROLLBACK TRANSACTION;
            RETURN;

        END


        /* =========================================
           5. Insert Home
           ========================================= */

        INSERT INTO [gmc].[tbl_Homes]
        (
            Home_Name,
            WelfareHomeType_ID,
            HomeCategory_ID,
            Registration_Number,
            Registration_Date,
            Establishment_Date,
            Current_Status,
            Managing_Organization,
            Year_Established,
            Home_Capacity,
            Current_Occupancy,
            CreatedOn,
            ModifiedOn
        )
        VALUES
        (
            @Home_Name,
            @WelfareHomeType_ID,
            @HomeCategory_ID,
            @Registration_Number,
            @Registration_Date,
            @Establishment_Date,
            @Current_Status,
            @Managing_Organization,
            @Year_Established,
            @Home_Capacity,
            @Current_Occupancy,
            GETDATE(),
            GETDATE()
        );


        DECLARE @Home_ID INT;

        SET @Home_ID = SCOPE_IDENTITY();


        /* =========================================
           6. Insert User
           
           UserType_ID = 2
           Welfare Homes
           ========================================= */

        INSERT INTO [gmc].[tbl_users]
        (
            FullName,
            Username,
            [Password],
            UserType_ID,
            Map_Home_ID,
            CreatedDate,
            Is_Active,
            LastLogin,
            Failed_Attempts,
            Last_Failed_Attempt,
            Lockout_Until
        )
        VALUES
        (
            @Home_Name,
            @Registration_Number,
            @Password,
            2,
            @Home_ID,
            GETDATE(),
            1,
            NULL,
            0,
            NULL,
            NULL
        );


        /* =========================================
           7. Success
           ========================================= */

        COMMIT TRANSACTION;

        SET @SuccessCode = '000';

        SET @SuccessMessage =
            'Welfare Home and User created successfully. Home ID: '
            + CAST(@Home_ID AS NVARCHAR(20));
            select @SuccessCode as 'Status',@SuccessMessage as 'Message'
    END TRY

    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @SuccessCode = '222';

        SET @SuccessMessage =
            ERROR_MESSAGE();
            select @SuccessCode as 'Status',@SuccessMessage as 'Message'
    END CATCH

END;
GO

IF OBJECT_ID(N'[gmc].[sp_SaveBillDeskTransaction]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[sp_SaveBillDeskTransaction] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[sp_SaveBillDeskTransaction]
    @OrderId NVARCHAR(50),
    @TransactionId NVARCHAR(50),
    @Status NVARCHAR(20),
    @AuthStatus NVARCHAR(10),
    @BankRefNo NVARCHAR(50),
    @Amount DECIMAL(18, 2),
    @PaymentMethod NVARCHAR(20),
    @TransactionDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

	DECLARE @ErrorCode VARCHAR(5) = '';
    DECLARE @TransStatus VARCHAR(5) ;


	BEGIN TRANSACTION

    INSERT INTO [gmc].[tbl_BillDeskTransactions]
    (
        OrderId,        TransactionId,        Status,        AuthStatus,        BankRefNo,
        Amount,        PaymentMethod,        TransactionDate,        CreatedAt
    )
    VALUES
    (
        @OrderId,        @TransactionId,        @Status,        @AuthStatus,        @BankRefNo,
        @Amount,        @PaymentMethod,        @TransactionDate,        GETDATE()
    )
	
	set @TransStatus='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @TransStatus AS 'Status','Status: '+ cast(@Status as varchar) +' ,Authentication Status: '+cast(@AuthStatus as varchar)+' ,PaymentMethod: '+@PaymentMethod as 'Message'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status',@ErrorCode AS 'Message' 
		ROLLBACK TRANSACTION
	END

END


--SELECT *  FROM [gmc].[tbl_BillDeskTransactions]
GO

IF OBJECT_ID(N'[gmc].[USP_Address_Add_ByHelpDesk]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Address_Add_ByHelpDesk] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Address_Add_ByHelpDesk]
@UserName	varchar(10) = null ,
@FullName	Nvarchar(200) = null,
@Family_Member_count	int = null,
@HNo		Nvarchar(100) = null,
@Building_Name  Nvarchar(100) = null,
@StreetOrLineNum nvarchar(50) = null,		
@Area_Name		Nvarchar(200) = null,
@LandMark		Nvarchar(200) = null,
@City		Nvarchar(100)  = null,
@Pincode		int  = null,
@Mobile		varchar(10) =null,
@WardID int=null,
@SecretariatID int=null,
@nickname varchar(100)=null

AS
BEGIN

--declare @Mobile bigint = 9502982197
--declare @OTP int = 123456


BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
DECLARE @UID nvarchar(11)
DECLARE @ID int




IF EXISTS (select 1 from [gmc].[TblAppUser] where UserName=@UserName )
BEGIN
       
	      SELECT @ID = ID from [gmc].[TblAppUser] where UserName=@UserName
		  print @ID	 
		  insert into [gmc].[tblAddress](UserID,HNo,Building_Name,StreetOrLineNum,Area_Name,
							LandMark,City,nickname,ModifiedBy,CreatedOn,AddressStatus,pincode,WardID,SecretariatID,mobile)
							values(@ID,@HNo,@Building_Name,@StreetOrLineNum,@Area_Name,@LandMark,'Guntur',@nickname,
							10001,GETDATE(),1,@pincode,@WardID,@SecretariatID,@mobile)

	 set @Status='000'
END
ELSE	
BEGIN
   set @Status='111'
 
 END  
   
  SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
	SELECT @Status AS 'Status'

	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
	    SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END

END

--select * from [gmc].[TblAppUser]
--select * from [gmc].[tblAddress]


--insert into [gmc].[TblAppUser](UserName,Mobile,IsActive)values(@Mobile,@Mobile,2)

--select DATEDIFF(SECOND,  '2024-12-04 14:17:08.640',getdate())

--select getdate()
GO

IF OBJECT_ID(N'[gmc].[USP_CitizenRegByHelpDesk]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_CitizenRegByHelpDesk] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_CitizenRegByHelpDesk]
@UserName	varchar(10) = null ,
@FullName	Nvarchar(200) = null,
@Family_Member_count	int = null,
@HNo		Nvarchar(100) = null,
@Building_Name  Nvarchar(100) = null,
@StreetOrLineNum nvarchar(50) = null,		
@Area_Name		Nvarchar(200) = null,
@LandMark		Nvarchar(200) = null,
@City		Nvarchar(100)  = null,
@Pincode		int  = null,
@Mobile		varchar(10) =null,
@WardID int=null,
@SecretariatID int=null,
@nickname varchar(100)=null

AS
BEGIN

--declare @Mobile bigint = 9502982197
--declare @OTP int = 123456


BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
DECLARE @UID nvarchar(11)
DECLARE @ID int




IF EXISTS (select 1 from [gmc].[TblAppUser] where UserName=@UserName )
BEGIN
       
       set @Status='111';
	   
END
ELSE	
BEGIN
    insert into [gmc].[TblAppUser](UserName,FullName,HNo,Building_Name,StreetOrLineNum,Area_Name,LandMark,City,Pincode,WardID,SecretariatID,Mobile,IsActive,CreatedOn,CreatedBy)values(@UserName,@FullName,@HNo,@Building_Name,@StreetOrLineNum,@Area_Name,@LandMark,@City,@Pincode,@WardID,@SecretariatID,@UserName,1,GETDATE(),10001)

	--select  @ID=ID,@UID=('GMC'+Right('00000000'+cast(ID as varchar(8)),8))    from [gmc].[TblAppUser] where UserName=@UserName  
	--   print @ID
	--   print @UID
	--   update [gmc].[TblAppUser] set UserName=@UserName,FullName=@FullName	,Family_Member_count=@Family_Member_count	,
    --   HNo=@HNo	,Building_Name=@Building_Name  ,StreetOrLineNum=@StreetOrLineNum ,
    --   Area_Name=@Area_Name	,LandMark=@LandMark	,City=@City	,Pincode=@Pincode,IsActive=1,ModifiedOn=GETDATE(),UID=@UID,WardID=@WardID,SecretariatID=@SecretariatID where UserName=@UserName

     SELECT @Id = SCOPE_IDENTITY();  
	 update [gmc].[TblAppUser] set UID= ('GMC'+Right('00000000'+cast(@Id as varchar(8)),8))    from [gmc].[TblAppUser] where ID=@Id
	 insert into [gmc].[tblAddress](UserID,HNo,Building_Name,StreetOrLineNum,Area_Name,
							LandMark,City,nickname,ModifiedBy,CreatedOn,AddressStatus,pincode,WardID,SecretariatID,mobile)
							values(@ID,@HNo,@Building_Name,@StreetOrLineNum,@Area_Name,@LandMark,'Guntur',@nickname,
							10001,GETDATE(),1,@pincode,@WardID,@SecretariatID,@mobile)

	 set @Status='000'
 
 END  
   
  SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
	SELECT @Status AS 'Status'

	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
	    SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END

END

--select * from [gmc].[TblAppUser]
--select * from [gmc].[tblAddress]


--insert into [gmc].[TblAppUser](UserName,Mobile,IsActive)values(@Mobile,@Mobile,2)

--select DATEDIFF(SECOND,  '2024-12-04 14:17:08.640',getdate())

--select getdate()
GO

IF OBJECT_ID(N'[gmc].[USP_CnD_PaymentConfirmedByEmp]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_CnD_PaymentConfirmedByEmp] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_CnD_PaymentConfirmedByEmp]
@CnDBookingID int null,
@EMPID int null,
@QtyInTons int null,
@TotalPrice decimal(18, 2) null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[tblCnDBookingDtls] where CnDBookingID=@CnDBookingID and CnDBookingStatus=1)
BEGIN
    
	   update [gmc].[tblCnDBookingDtls] set CnDBookingStatus=2,PaymentConfirmedBy=@EMPID,PaymentConfirmedOn=GETDATE(),QtyInTons=@QtyInTons,TotalPrice=@TotalPrice where CnDBookingID=@CnDBookingID
	
	set @Status='000'
END
ELSE
BEGIN
    set @Status='111'

END


SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END


--EXEC [gmc].[USP_IU_CnD_Assignment] @CnDBookingID=1,@EMPID=10000,@TruckDriverID =1
GO

IF OBJECT_ID(N'[gmc].[USP_Data_For_Amaravathi]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Data_For_Amaravathi] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Data_For_Amaravathi]
@Flag varchar(50)=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null,
@RMonth int=null,
@RYear int=null,
@valueFlag varchar(50)=null,
@SectionID int=null

AS
BEGIN

set nocount on;

--Section-wise data retrieval
--Date-wise data retrieval
--Month-wise data retrieval
--Range-based data retrieval (from a specific start date to an end date)


--******************************************************
--EXEC [gmc].[USP_Data_For_Amaravathi] @Flag='daterange',@RMonth=4,@RYear=2025,@SectionID=5,@FDate='01/07/2025',@TDate='11/07/2025'
--Section=5&ValueFlag=Sec_Tot&fromDate=&toDate=&month=5&year=2025&F=monthyear
Declare @CurrentDate datetime=getdate();
Declare @FromDate datetime=convert(datetime,@FDate,103)
Declare @ToDate datetime=convert(datetime,@TDate,103)

        IF @Flag='Sectionwise'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,c.SubGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				
				--a.Description,
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription,
				case when a.SectionID=1 then 'URBAN POVERTY ALLEVIATION CELL'
				     when a.SectionID=2 then 'PUBLIC HEALTH SECTION'
					 when a.SectionID=3 then 'REVENUE SECTION'
					 when a.SectionID=4 then 'Town Planing'
					 when a.SectionID=5 then 'ENGINEERING SECTION'
					 else 'Others' end as 'Section'
				from [gmc].[TblUserGrievance] a with(nolock)
				left join [gmc].[TblAppUser] u with(nolock) on u.ID=a.UserID
				left join [gmc].[tblAddress] ad with(nolock) on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b with(nolock) on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c with(nolock) on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m with(nolock) on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  
				   OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				
				order by a.UserGrievanceID
			  END
	 IF @Flag='datewise'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,c.SubGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription,
				case when a.SectionID=1 then 'URBAN POVERTY ALLEVIATION CELL'
				     when a.SectionID=2 then 'PUBLIC HEALTH SECTION'
					 when a.SectionID=3 then 'REVENUE SECTION'
					 when a.SectionID=4 then 'Town Planing'
					 when a.SectionID=5 then 'ENGINEERING SECTION'
					else 'Others' end as 'Section'
				from [gmc].[TblUserGrievance] a with(nolock)
				left join [gmc].[TblAppUser] u with(nolock) on u.ID=a.UserID
				left join [gmc].[tblAddress] ad with(nolock) on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b with(nolock) on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c with(nolock) on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m with(nolock) on  m.EMPID=a.ApprovedBy
				where 
				CAST(a.CreatedOn AS DATE)= CAST(@FromDate as date) 
				order by a.UserGrievanceID asc
			  END  
      IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,c.SubGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription,
				case when a.SectionID=1 then 'URBAN POVERTY ALLEVIATION CELL'
				     when a.SectionID=2 then 'PUBLIC HEALTH SECTION'
					 when a.SectionID=3 then 'REVENUE SECTION'
					 when a.SectionID=4 then 'Town Planing'
					 when a.SectionID=5 then 'ENGINEERING SECTION'
					 else 'Others' end as 'Section'
				from [gmc].[TblUserGrievance] a with(nolock)
				left join [gmc].[TblAppUser] u with(nolock) on u.ID=a.UserID
				left join [gmc].[tblAddress] ad with(nolock) on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b with(nolock) on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c with(nolock) on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m with(nolock) on  m.EMPID=a.ApprovedBy
				where 
				 year(a.CreatedOn)=@RYear and  month(a.CreatedOn)=@RMonth
				order by a.UserGrievanceID asc
			  END  
          else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,c.SubGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription,
				case when a.SectionID=1 then 'URBAN POVERTY ALLEVIATION CELL'
				     when a.SectionID=2 then 'PUBLIC HEALTH SECTION'
					 when a.SectionID=3 then 'REVENUE SECTION'
					 when a.SectionID=4 then 'Town Planing'
					 when a.SectionID=5 then 'ENGINEERING SECTION'
					 else 'Others' end as 'Section'
				from [gmc].[TblUserGrievance] a with(nolock)
				left join [gmc].[TblAppUser] u with(nolock) on u.ID=a.UserID
				left join [gmc].[tblAddress] ad with(nolock) on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b with(nolock) on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c with(nolock) on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m with(nolock) on  m.EMPID=a.ApprovedBy
				where CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				order by a.UserGrievanceID asc
			  END  

END
GO

IF OBJECT_ID(N'[gmc].[USP_Deactivate_Beneficiary]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Deactivate_Beneficiary] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Deactivate_Beneficiary]
(
    @BeneficiaryID INT,
    @Remarks varchar(100)=null
)
AS
BEGIN
    SET NOCOUNT ON;


       declare @SuccessCode varchar(3) 
     declare @SuccessMessage NVARCHAR(500)

    BEGIN TRY

        /* =========================================
           Check Beneficiary
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryRegistration]
            WHERE BeneficiaryID = @BeneficiaryID
        )
        BEGIN
            SET @SuccessCode = '111';
            SET @SuccessMessage = 'Beneficiary not found.';

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                @BeneficiaryID AS BeneficiaryID;

            RETURN;
        END


        /* =========================================
           Check Already Inactive
           ========================================= */

        IF EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryRegistration]
            WHERE BeneficiaryID = @BeneficiaryID
              AND Is_Active = 0
        )
        BEGIN
            SET @SuccessCode = '111';
            SET @SuccessMessage = 'Beneficiary is already inactive.';

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                @BeneficiaryID AS BeneficiaryID;

            RETURN;
        END


        /* =========================================
           Deactivate Beneficiary
           ========================================= */

        UPDATE [gmc].[tbl_BeneficiaryRegistration]
        SET
            Is_Active = 0,
            DeactivatedRemarks=@Remarks,
            DeactivatedOn = GETDATE()
        WHERE BeneficiaryID = @BeneficiaryID;


        /* =========================================
           Success
           ========================================= */

        SET @SuccessCode = '000';
        SET @SuccessMessage =
            'Beneficiary deactivated successfully.';


        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            @BeneficiaryID AS BeneficiaryID;

    END TRY

    BEGIN CATCH

        SET @SuccessCode = '222';
        SET @SuccessMessage = ERROR_MESSAGE();

        SELECT
            @SuccessCode AS  'Status',
            @SuccessMessage AS 'Message',
            @BeneficiaryID AS BeneficiaryID;

    END CATCH

END;
GO

IF OBJECT_ID(N'[gmc].[USP_EMP_Master_List]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_EMP_Master_List] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_EMP_Master_List]
@EMPID int=null,
@Flag varchar(500)=null,
@CnDBookingStatus int=null,
@Date varchar(10)=null,
@Secretariat int=null,
@Section  int=null,
@Uname varchar(20)=null

AS
BEGIN

set nocount on;


Declare @SectionID int
Declare @SecretariatID int
Declare @Role varchar(20)
Declare @UserType int

select @SectionID=Section,@Role=Role,@UserType=UserType  from [gmc].[TblEMPUser] with(nolock) where EMPID=@EMPID and IsActive=1
print @SectionID
print @Role
print @UserType



IF @Flag='GetEmpByEMP'
BEGIN
    select EMPID,FullName,Mobile,s.SectionID,case when s.SectionID is null then null else  s.SectionName end as 'Section' ,case when IsActive=1 then 'Active' else 'Inactive' end as 'EmpStatus' 
	,e.Role,u.UserTypeID as 'UserType',u.UserTypeName
	from [gmc].[TblEMPUser] e
	left join [gmc].[TblSections] s on e.Section=s.SectionID
	left join [gmc].[TblUserType] u on u.UserTypeID=e.UserType
	where EMPID=@EMPID

END

IF @Flag='GetGrievanceForEMP'
BEGIN

			select  'Data found' as 'Status',a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' else 'Completed' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
		      order by a.UserID, a.GrievanceStatus asc
END

IF @Flag='GetGrievance_StatusCountForEMP'
BEGIN

			select ( select count(UserGrievanceID) from [gmc].[TblUserGrievance]) as 'TotalGrievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] where GrievanceStatus=1) as 'Total_Active_Grievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance]  where GrievanceStatus!=1) as 'Total_Completed_Grievances',
			0 as 'TotalInProcess'		      
END

IF @Flag='GetCndBookingDtlsForEmp'
BEGIN
  select 
a.CnDBookingID,
a.WasteCategoryID,c.WasteCategoryName as 'WasteCategory',a.WasteTypeID,t.WasteTypeName as 'WasteType',
a.QtyInTons ,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
a.BookingTime,
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
a.CnDBookingStatus,
case when a.CnDBookingStatus=1 then 'Trip booked, ward secretary is confirm the amount for trip' when a.CnDBookingStatus=2 then 'Payment is pending please complete payment'  
                               --when a.CnDBookingStatus=3 then 'Payment Completed By' 
                                 else 'NA' end as 'CnDBookingStatusText',
--case when a.CnDBookingStatus=1 then 'Active' when a.CnDBookingStatus=2 then 'Processed'  when a.CnDBookingStatus=3 then 'Completed' else 'NA' end as 'CnDBookingStatus',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

a.Langitude,a.Latitude from [gmc].[tblCnDBookingDtls] a
inner join 		
[gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID
inner join [gmc].[TblWasteCategory] c on c.WasteCategoryID=a.WasteCategoryID
inner join [gmc].[TblWasteType] t on t.WasteTypeID=a.WasteTypeID
where  a.AddressID=b.AddressID order by a.CreatedOn Desc
		
END

IF @Flag='AvailableTruckDrivers'
BEGIN
select TruckDriverID,DriverName,TruckNum,DriverMobile,case when AvailabilityStatus=1 then 'Available' else 'Not Available' end as 'AvailabilityStatus' from [gmc].[TblCnDTruckDriverDtls] with(nolock) where  ISActive=1
END

IF @Flag='TruckDriverDetlsByTruckDriver'
BEGIN
select TruckDriverID,DriverName,TruckNum,DriverMobile from [gmc].[TblCnDTruckDriverDtls] with(nolock) where   ISActive=1
END

IF @Flag='getELSR'
BEGIN
    
	
DECLARE @MidUser int

;with CTE AS( SELECT s.* FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u on u.EMPID=s.EMPID and s.EMPID=u.EMPID
where u.Section=5 and u.UserType in(24,25))

,GetEMPID AS(SELECT  DISTINCT  m.EMPID AS MidLevelEmpID
FROM CTE m
JOIN CTE e
    ON m.SecretariatID = e.SecretariatID
WHERE e.EMPID = @EmpID
  AND m.Role = 'MidLevel')

 select @MidUser=MidLevelEmpID from GetEMPID
 print @MidUser


    select ELSRID,ELSR_Location,ELSR_Capacity,AE_Code,Source,Capacity from [gmc].[tbl_ELSR] where AE_ID=@MidUser
	select * from [gmc].[tbl_ELSR_TimeSlats]
END

IF @Flag='GetEmpELSR'
BEGIN
	--select a.RCTestingID,a.ImageBase64,a.Description,a.CreateOn,b.ELSR_Location,b.ELSR_Capacity,
	--cast(day(a.CreateOn) as varchar) as 'Day',cast(FORMAT(a.CreateOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreateOn) as varchar),2) as 'MonthYear'
	--from [gmc].[tbl_RCTesting] a inner join [gmc].[tbl_ELSR] b on a.ELSRID=b.ELSRID where a.CreatedBy=
	;with cte as(
select a.RCTestingID,a.ImageBase64,a.Description,a.CreateOn,a.Result,b.ELSR_Location,b.ELSR_Capacity,
	cast(day(a.CreateOn) as varchar) as 'Day',cast(FORMAT(a.CreateOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreateOn) as varchar),2) as 'MonthYear',
	m.SecretariatID
	,
	 ROW_NUMBER() OVER (PARTITION BY a.RCTestingID ORDER BY m.EMPID) AS rn
	from [gmc].[tbl_RCTesting] a inner join [gmc].[vw_RCTesting] v on a.RCTestingID=v.RCTestingID
	inner join [gmc].[tbl_ELSR] b on a.ELSRID=b.ELSRID 
    inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=v.SecretariatID 
    where m.EMPID=@EMPID
	)
select * from cte where rn=1

	
	
END

IF @Flag='GetCndBookingDtlsForEmpByEMPIDandCnDStatus'
BEGIN
 ;with CTE AS( select 
a.CnDBookingID,
a.WasteCategoryID,c.WasteCategoryName as 'WasteCategory',a.WasteTypeID,t.WasteTypeName as 'WasteType',
a.QtyInTons ,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
a.BookingTime,
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
a.CnDBookingStatus,
case when a.CnDBookingStatus=1 then 'Trip booked, ward secretary is confirm the amount for trip' when a.CnDBookingStatus=2 then 'Payment is pending please complete payment'  
                                when a.CnDBookingStatus=3 then 'Payment Completed By User' 
                                 else 'NA' end as 'CnDBookingStatusText',
--case when a.CnDBookingStatus=1 then 'Active' when a.CnDBookingStatus=2 then 'Processed'  when a.CnDBookingStatus=3 then 'Completed' else 'NA' end as 'CnDBookingStatus',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

a.Langitude,a.Latitude,b.nickname,b.mobile,
 ROW_NUMBER() OVER (PARTITION BY a.CnDBookingID ORDER BY e.EMPID) AS rn
from [gmc].[tblCnDBookingDtls] a
inner join [gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID and b.AddressID=a.AddressID
inner join [gmc].[TblWasteCategory] c on c.WasteCategoryID=a.WasteCategoryID
inner join [gmc].[TblWasteType] t on t.WasteTypeID=a.WasteTypeID
inner join [gmc].[TblSecretariat] s on s.SecretariatID=b.SecretariatID
inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=b.SecretariatID 
inner join [gmc].[TblEMPUser] e on e.EMPID=m.EMPID 
where m.EMPID=@EMPID and a.CnDBookingStatus=@CnDBookingStatus and e.Section=4 
)

select CnDBookingID,	WasteCategoryID,	WasteCategory,	WasteTypeID,	WasteType,	QtyInTons,	TotalPrice,	BookingDate,	BookingTime,	Day,	MonthYear,	CnDBookingStatus,	CnDBookingStatusText,	AddressID,	Address,	Langitude,	Latitude,	nickname,	mobile
from CTE WHERE rn = 1
		
END

IF @Flag='GetCndBookingDtls_Rpt_ForEmp'
BEGIN

 ;with CTE AS( select 
a.CnDBookingID,
a.WasteCategoryID,
a.CnDBookingStatus,
 ROW_NUMBER() OVER (PARTITION BY a.CnDBookingID ORDER BY e.EMPID) AS rn
from [gmc].[tblCnDBookingDtls] a
inner join [gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID and b.AddressID=a.AddressID
inner join [gmc].[TblSecretariat] s on s.SecretariatID=b.SecretariatID
inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=b.SecretariatID 
inner join [gmc].[TblEMPUser] e on e.EMPID=m.EMPID 
where m.EMPID=@EMPID  and e.Section=4 
)

,final as (
select CnDBookingID,	WasteCategoryID, CnDBookingStatus
from CTE WHERE rn = 1)
select 
      (select count(CndBookingID) from final ) as 'Tot_CnD',
	  (select count(CndBookingID) from final where CnDBookingStatus=1) as 'Tot_Active',
	  (select count(CndBookingID) from final where CnDBookingStatus=2) as 'Tot_PayemtUpdateByEmp',
	  (select count(CndBookingID) from final where CnDBookingStatus=3) as 'Tot_PaymentCompledByUser',
	  (select count(CndBookingID) from final where CnDBookingStatus=4) as 'Tot_BookingCompleted'
END

IF @Flag='GET_ELSR_FilldDtls_For_AE'
BEGIN

   
	IF @Role='MidLevel'
	BEGIN
		select a.ELSRFilledID,b.ELSR_Location,convert(varchar(10),a.FIlledDate,103) as 'Date',
		cast(day(a.FIlledDate) as varchar) as 'Day',cast(FORMAT(a.FIlledDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.FIlledDate) as varchar),2) as 'MonthYear',
		b.ELSR_Capacity,c.TiemSlat,a.Noof_KL_Filled,a.Remaks as 'Remarks' from [gmc].[tbl_ELSR_FilledDtls] a
		left join [gmc].[tbl_ELSR] b on a.ELSRID=b.ELSRID
		left join [gmc].[tbl_ELSR_TimeSlats] c on c.ELSRTimeSlatID=a.ELSRTimeSlatID
		where FilledDate=convert(date,@Date,103) and b.AE_ID=@EMPID order by a.CreatedOn desc
     END
	 ELSE
	 BEGIN
	     select a.ELSRFilledID,b.ELSR_Location,convert(varchar(10),a.FIlledDate,103) as 'Date',
		 cast(day(a.FIlledDate) as varchar) as 'Day',cast(FORMAT(a.FIlledDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.FIlledDate) as varchar),2) as 'MonthYear',
		 b.ELSR_Capacity,c.TiemSlat,a.Noof_KL_Filled,a.Remaks as 'Remarks' from [gmc].[tbl_ELSR_FilledDtls] a
		left join [gmc].[tbl_ELSR] b on a.ELSRID=b.ELSRID
		left join [gmc].[tbl_ELSR_TimeSlats] c on c.ELSRTimeSlatID=a.ELSRTimeSlatID
		where FilledDate=convert(date,@Date,103)  order by a.CreatedOn desc
	 END
END
IF @Flag='GetEmpDtlsForAppUser'
BEGIN
	select e.FullName,e.Mobile,t.UserTypeName as 'Designation',w.WardName,s.SecretariatName from [gmc].[TblEMPUser] e
			inner join [gmc].[tblEMPMappingSecretariat] b on e.EMPID=b.EMPID
			inner join [gmc].[TblSecretariat] s on s.SecretariatID=b.SecretariatID
			inner join [gmc].[TblWards] w on w.WardID=s.WardID
			inner join [gmc].[TblUserType] t on e.UserType=t.UserTypeID and e.Section=t.SectionID
			where b.SecretariatID=@Secretariat and e.Section=@Section 
END
IF @Flag='GetSections'
BEGIN
	select SectionID,SectionName from [gmc].[TblSections]

END

IF @Flag='GetEmpName'
BEGIN
	select top 1 case when FullName is null then 'Citizen Registration Pending' else FullName end as 'FullName' from [gmc].[TblAppUser] where UserName=@Uname

END

END

--EXEC [gmc].[USP_EMP_Master_List] @Flag='GetCndBookingDtlsForEmpByEMPIDandCnDStatus',@EMPID=10293,@CnDBookingStatus=1

--EXEC [gmc].[USP_EMP_Master_List] @Flag='GET_ELSR_FilldDtls_For_AE',@EMPID=10194,@Date='07/05/2025'
GO

IF OBJECT_ID(N'[gmc].[USP_GarbageCollection_PaymentConfirmedByEmp]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_GarbageCollection_PaymentConfirmedByEmp] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_GarbageCollection_PaymentConfirmedByEmp]
@EGCollectionID int null,
@EMPID int null,
@NoofTrips int null,
@TotalPrice decimal(18, 2) null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[tbl_EventGarbageCollection] where EGCollectionID=@EGCollectionID and EGCollectionStatus=1)
BEGIN
    
	   update [gmc].[tbl_EventGarbageCollection] set EGCollectionStatus=2,PaymentConfirmedBy=@EMPID,PaymentConfirmedOn=GETDATE(),NoofTrips=@NoofTrips,TotalPrice=@TotalPrice where EGCollectionID=@EGCollectionID
	
	set @Status='000'
END
ELSE
BEGIN
    set @Status='111'

END


SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END


--EXEC [gmc].[USP_IU_CnD_Assignment] @CnDBookingID=1,@EMPID=10000,@TruckDriverID =1
GO

IF OBJECT_ID(N'[gmc].[USP_Get_AdminDashboardCounts]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_AdminDashboardCounts] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_AdminDashboardCounts]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT

        /* =========================================
           Total Active Homes
           ========================================= */
        (
            SELECT COUNT(*)
            FROM [gmc].[tbl_Homes]        
        ) AS TotalHomes,


        /* =========================================
           Total Home Capacity
           ========================================= */
        (
            SELECT ISNULL(SUM(ISNULL(Home_Capacity, 0)), 0)
            FROM [gmc].[tbl_Homes]       
        ) AS TotalHomeCapacity,


        /* =========================================
           Total Beneficiary Registered
           ========================================= */
        (
            SELECT COUNT(*)
            FROM [gmc].[tbl_BeneficiaryRegistration] b
            INNER JOIN [gmc].[tbl_Homes] h
                ON h.Home_ID = b.Home_ID
            WHERE b.Is_Active = 1
              
        ) AS TotalBeneficiaryRegistered,


        /* =========================================
           Total FRS Registered
           ========================================= */
        (
            SELECT COUNT(*)
            FROM [gmc].[tbl_BeneficiaryRegistration] b
            INNER JOIN [gmc].[tbl_Homes] h
                ON h.Home_ID = b.Home_ID
            WHERE b.Is_Active = 1
              
              AND b.FRSStatus = 1
        ) AS TotalBeneficiaryFRSRegistered,


        /* =========================================
           Total Male
           ========================================= */
        (
            SELECT COUNT(*)
            FROM [gmc].[tbl_BeneficiaryRegistration] b
            INNER JOIN [gmc].[tbl_Homes] h
                ON h.Home_ID = b.Home_ID
            WHERE b.Is_Active = 1
              
              AND b.Gender = 'Male'
        ) AS TotalMale,


        /* =========================================
           Total Female
           ========================================= */
        (
            SELECT COUNT(*)
            FROM [gmc].[tbl_BeneficiaryRegistration] b
            INNER JOIN [gmc].[tbl_Homes] h
                ON h.Home_ID = b.Home_ID
            WHERE b.Is_Active = 1
              
              AND b.Gender = 'Female'
        ) AS TotalFemale,


        /* =========================================
           Today's Attendance
           Unique Beneficiaries
           ========================================= */
        (
            SELECT COUNT(DISTINCT a.BeneficiaryID)
            FROM [gmc].[tbl_BeneficiaryAttendance] a

            INNER JOIN [gmc].[tbl_Homes] h
                ON h.Home_ID = a.Home_ID

            INNER JOIN [gmc].[tbl_BeneficiaryRegistration] b
                ON b.BeneficiaryID = a.BeneficiaryID

            WHERE a.AttendanceDate = CAST(GETDATE() AS DATE)
              AND a.Is_Active = 1
              
              AND b.Is_Active = 1
        ) AS TotalAttendance;

END;
GO

IF OBJECT_ID(N'[gmc].[USP_Get_BeneficiaryAttendanceToday]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_BeneficiaryAttendanceToday] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_BeneficiaryAttendanceToday]
(
    @Home_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    --exec [gmc].[USP_Get_BeneficiaryAttendanceToday] 1

    SELECT
        b.BeneficiaryID,
        b.Name,
        b.Gender,
        b.DOB,
        b.Age,
        b.FatherMotherGuardianName,
        b.MaritalStatus,
        b.MobileNumber,
        b.Aadhaar,

        -- FRS Details
        b.ImagePath,
        b.ImageFileName,
        b.FRSStatus,
        b.ImageTemplate,

        b.Home_ID,

        -- Attendance Date
        CAST(GETDATE() AS DATE) AS AttendanceDate,

        -- Morning Attendance
        ma.AttendanceID AS MorningAttendanceID,
        ma.AttendanceTime AS MorningAttendanceTime,
        ma.Latitude AS MorningLatitude,
        ma.Longitude AS MorningLongitude,
       

        -- Evening Attendance
        ea.AttendanceID AS EveningAttendanceID,
        ea.AttendanceTime AS EveningAttendanceTime,
        ea.Latitude AS EveningLatitude,
        ea.Longitude AS EveningLongitude,

                CASE 
        WHEN CAST(GETDATE() AS TIME) < '14:00:00'
        THEN 'M'
        ELSE 'E'
        END AS AttendanceType
    

    FROM [gmc].[tbl_BeneficiaryRegistration] b

    /* =========================================
       Morning Attendance
       ========================================= */

    LEFT JOIN [gmc].[tbl_BeneficiaryAttendance] ma
        ON ma.BeneficiaryID = b.BeneficiaryID
        AND ma.Home_ID = b.Home_ID
        AND ma.AttendanceDate = CAST(GETDATE() AS DATE)
        AND ma.AttendanceType = 'M'
        AND ma.Is_Active = 1

    /* =========================================
       Evening Attendance
       ========================================= */

    LEFT JOIN [gmc].[tbl_BeneficiaryAttendance] ea
        ON ea.BeneficiaryID = b.BeneficiaryID
        AND ea.Home_ID = b.Home_ID
        AND ea.AttendanceDate = CAST(GETDATE() AS DATE)
        AND ea.AttendanceType = 'E'
        AND ea.Is_Active = 1

    WHERE b.Home_ID = @Home_ID
      AND b.Is_Active = 1
      AND b.FRSStatus=1

    ORDER BY b.Name;

END;
GO

IF OBJECT_ID(N'[gmc].[USP_Get_EventGarbageDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_EventGarbageDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_EventGarbageDtls]
@UserID int=null,
@Flag varchar(50)=null,
@EMPID int=null,
@EGCollectionStatus int=null

As


set nocount on
--EXEC [gmc].[USPGetBookWaterTanker] 1


BEGIN


IF @Flag='GetEventGarbageDtlsForUser'
BEGIN
select a.EGCollectionID,
case 
when EventTypeID=1 then 'Meetings'
when EventTypeID=2 then	'Parties'
when EventTypeID=3 then 'Functions' end as 'NameOftheEvent',
case
when EventTimeID=1	then 'Morning 8am - 11am'
when EventTimeID=2	then 'Afternoon 12pm - 3pm'
when EventTimeID=3	then 'Evening 3pm - 6pm'
when EventTimeID=4	then 'Night 7pm - 11pm' end as 'TimeOfTheEvent',
case
when EventnoofMemberId=1	then 'Above 100'
when EventnoofMemberId=2	then 'Below 200'
when EventnoofMemberId=3	then 'Below 300'
when EventnoofMemberId=4	then 'Below 500' end as 'NoofPeopleParticipate',
a.NoofTrips,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',

cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
case when a.EGCollectionStatus=1 then '1' when a.EGCollectionStatus=2 then '2'when a.EGCollectionStatus=3 then '3'when a.EGCollectionStatus=4 then '4'when a.EGCollectionStatus=5 then '5' else '0' end as 'EGCollectionStatus',
case when a.EGCollectionStatus=1 then 'Garbage Collection booked, ward secretary is confirm the amount for Event Garbage Collection' when a.EGCollectionStatus=2 then 'Payment is pending please complete payment'  
                               --when a.CnDBookingStatus=3 then 'Payment Completed By' 
                                 else 'NA' end as 'EGCollectionStatusText',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

a.Langitude,a.Latitude from [gmc].[tbl_EventGarbageCollection] a
inner join 		
[gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID
where a.UserID=@UserID  and a.AddressID=b.AddressID order by a.CreatedOn Desc


END


IF @Flag='GetGarbageCollectionDtlsForEmp'
BEGIN
select a.EGCollectionID,
case 
when EventTypeID=1 then 'Meetings'
when EventTypeID=2 then	'Parties'
when EventTypeID=3 then 'Functions' end as 'NameOftheEvent',
case
when EventTimeID=1	then 'Morning 8am - 11am'
when EventTimeID=2	then 'Afternoon 12pm - 3pm'
when EventTimeID=3	then 'Evening 3pm - 6pm'
when EventTimeID=4	then 'Night 7pm - 11pm' end as 'TimeOfTheEvent',
case
when EventnoofMemberId=1	then 'Above 100'
when EventnoofMemberId=2	then 'Below 200'
when EventnoofMemberId=3	then 'Below 300'
when EventnoofMemberId=4	then 'Below 500' end as 'NoofPeopleParticipate',
a.NoofTrips,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
a.EGCollectionStatus,
case when a.EGCollectionStatus=1 then 'Trip booked, ward secretary is confirm the amount for trip' when a.EGCollectionStatus=2 then 'Payment is pending please complete payment'  
                               --when a.CnDBookingStatus=3 then 'Payment Completed By' 
                                 else 'NA' end as 'EGCollectionStatusText',
--case when a.CnDBookingStatus=1 then 'Active' when a.CnDBookingStatus=2 then 'Processed'  when a.CnDBookingStatus=3 then 'Completed' else 'NA' end as 'CnDBookingStatus',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

b.Langitude,b.Latitude from [gmc].[tbl_EventGarbageCollection] a
inner join 		
[gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID
where  a.AddressID=b.AddressID order by a.CreatedOn Desc
		
END


IF @Flag='GetGarbageCollectionDtlsEmpByEMPIDandEGStatus'
BEGIN
 ;with CTE AS( select a.EGCollectionID,
case 
when EventTypeID=1 then 'Meetings'
when EventTypeID=2 then	'Parties'
when EventTypeID=3 then 'Functions' end as 'NameOftheEvent',
case
when EventTimeID=1	then 'Morning 8am - 11am'
when EventTimeID=2	then 'Afternoon 12pm - 3pm'
when EventTimeID=3	then 'Evening 3pm - 6pm'
when EventTimeID=4	then 'Night 7pm - 11pm' end as 'TimeOfTheEvent',
case
when EventnoofMemberId=1	then 'Above 100'
when EventnoofMemberId=2	then 'Below 200'
when EventnoofMemberId=3	then 'Below 300'
when EventnoofMemberId=4	then 'Below 500' end as 'NoofPeopleParticipate',
a.NoofTrips,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
a.EGCollectionStatus,
case when a.EGCollectionStatus=1 then 'Trip booked, ward secretary is confirm the amount for trip' when a.EGCollectionStatus=2 then 'Payment is pending please complete payment'  
                                when a.EGCollectionStatus=3 then 'Payment Completed By User' 
                                 else 'NA' end as 'EGCollectionStatusText',
--case when a.CnDBookingStatus=1 then 'Active' when a.CnDBookingStatus=2 then 'Processed'  when a.CnDBookingStatus=3 then 'Completed' else 'NA' end as 'CnDBookingStatus',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

b.Langitude,b.Latitude,b.nickname,b.mobile,
 ROW_NUMBER() OVER (PARTITION BY a.EGCollectionID ORDER BY e.EMPID) AS rn
from [gmc].[tbl_EventGarbageCollection] a
inner join [gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID and b.AddressID=a.AddressID
inner join [gmc].[TblSecretariat] s on s.SecretariatID=b.SecretariatID
inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=b.SecretariatID 
inner join [gmc].[TblEMPUser] e on e.EMPID=m.EMPID 
where m.EMPID=@EMPID and a.EGCollectionStatus=@EGCollectionStatus and e.Section=2 
)

select EGCollectionID,	NameOftheEvent,	TimeOfTheEvent,	NoofPeopleParticipate,		NoofTrips,	TotalPrice,	BookingDate,		Day,	MonthYear,	EGCollectionStatus,	EGCollectionStatusText,	AddressID,	Address,	Langitude,	Latitude,	nickname,	mobile
from CTE WHERE rn = 1
		
END

IF @Flag='GetGarbageCollectionDtls_Rpt_Counts_ForEmp'
BEGIN

 ;with CTE AS( select 
a.EGCollectionID,
a.EGCollectionStatus,
 ROW_NUMBER() OVER (PARTITION BY a.EGCollectionID ORDER BY e.EMPID) AS rn
from [gmc].[tbl_EventGarbageCollection] a
inner join [gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID and b.AddressID=a.AddressID
inner join [gmc].[TblSecretariat] s on s.SecretariatID=b.SecretariatID
inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=b.SecretariatID 
inner join [gmc].[TblEMPUser] e on e.EMPID=m.EMPID 
where m.EMPID=@EMPID  and e.Section=2
)

,final as (
select EGCollectionID, EGCollectionStatus
from CTE WHERE rn = 1)
select
   (select count(EGCollectionID) from final ) as 'Tot_EGCollections',
   (select count(EGCollectionID) from final where EGCollectionStatus=1) as 'Tot_Active',
   (select count(EGCollectionID) from final where EGCollectionStatus=2) as 'Tot_PayemtUpdateByEmp',
   (select count(EGCollectionID) from final where EGCollectionStatus=3) as 'Tot_PaymentCompledByUser',
   (select count(EGCollectionID) from final where EGCollectionStatus=3) as 'Tot_BookingCompleted'

END



END

--select * from [gmc].[TblWasteCategory]
--select * from [gmc].[tbl_EventTypes]
--select * from [gmc].[tbl_EventTimes]
--select * from [gmc].[tbl_EventParticipationMembers]
GO

IF OBJECT_ID(N'[gmc].[USP_GET_Grievance_DataForEmpUser]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_GET_Grievance_DataForEmpUser] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_GET_Grievance_DataForEmpUser]
@EMPID int=null,
@Flag varchar(500)=null,
@GrievanceStatus int=null,
@UserGrievanceID int=null

AS
BEGIN

--**************************************
--Declare @EMPID int=10299
--Declare @Flag varchar(500)='GetGrievanceForEMP'
--EXEC [gmc].[USP_GET_Grievance_DataForEmpUser] @EMPID =10293,@Flag ='GetGrievanceForEMP'
--**************************************

set nocount on;

Declare @SectionID int
Declare @SecretariatID int
Declare @Role varchar(20)
Declare @UserType int

select @SectionID=Section,@Role=Role,@UserType=UserType  from [gmc].[TblEMPUser] with(nolock) where EMPID=@EMPID and IsActive=1
print @SectionID
print @Role
print @UserType


IF @Flag='GetGrievanceForEMP'
BEGIN

   IF @Role='SectionAdmin' 
   BEGIN
			  select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  where a.SectionID=@SectionID
		      order by a.UserID, a.GrievanceStatus asc
	END
	ELSE IF  @Role='SubAdmin'
	BEGIN
	     select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
		      order by a.UserID, a.GrievanceStatus asc
	END
	ELSE IF  @Role='MidLevel'
	BEGIN
	     select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
		      order by a.UserID, a.GrievanceStatus asc
	END
	ELSE IF  @Role='EndLevel'
	BEGIN
	     select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
		      order by a.UserID, a.GrievanceStatus asc
	END
END

IF @Flag='GetGrievanceForEndLevelEmpForUpdate'
BEGIN
		select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
			  and a.GrievanceStatus=1
		      order by a.UserID, a.GrievanceStatus asc

END

IF @Flag='GetGrievanceForMidLevelEmpForApproval'
BEGIN
		select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
			  and a.GrievanceStatus=2
		      order by a.UserID, a.GrievanceStatus asc

END


IF @Flag='GetGrievanceListForEmployee'
BEGIN
		select a.UserGrievanceID , a.UserID,u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,
			  --a.ImageBase64,
			  a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  --a.EMPImageBase64,
			  a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
			  left join [gmc].[TblAppUser] u on u.ID=a.UserID
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.SectionID=@SectionID and m.EMPID=@EMPID
			  and a.GrievanceStatus=@GrievanceStatus
		      order by a.UserGrievanceID, a.UserID, a.GrievanceStatus asc

END



IF @Flag='GetGrievanceByGrievenceID'
BEGIN
		select  a.UserGrievanceID , a.UserID,u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',a.MainGrievanceID,b.MainGrievanceName,
		       ad.HNo,ad.Building_Name,ad.StreetOrLineNum,ad.Area_Name,ad.LandMark,ad.nickname,ad.Langitude,ad.Latitude,ad.pincode,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,
			  a.ImageBase64 as 'BefireImage',
			  a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64 as 'AfterImage',
			  a.EMPUpdatedDate,a.EMPDescription,
			    (SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'Secretery_Name',
              (SELECT Top 1 u1.Mobile FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'Secretery_Mobile'
		      from [gmc].[TblUserGrievance] a 
			  inner join [gmc].[TblAppUser] u on u.ID=a.UserID
			  inner join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
		      inner join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      inner join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  --inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  where a.UserGrievanceID=@UserGrievanceID
			  order by a.UserID, a.GrievanceStatus asc

END

END


--EXEC [gmc].[USP_GET_Grievance_DataForEmpUser] @UserGrievanceID=31,@Flag ='GetGrievanceByGrievenceID'

--EXEC [gmc].[USP_EMP_Master_List] @Flag='GetGrievance_StatusCountForEMP'

--select * from [gmc].[TblUserGrievance] where SectionID=4
--select * from [gmc].[tblEMPMappingSecretariat]
--select distinct Role from [gmc].[TblEMPUser]

--select * from [gmc].[TblAppUser]

--select * from [gmc].[tblAddress] where AddressID=14
GO

IF OBJECT_ID(N'[gmc].[USP_Get_HomeDashboardCounts]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_HomeDashboardCounts] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_HomeDashboardCounts]
(
    @Home_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    --EXEC [gmc].[USP_Get_HomeDashboardCounts] @Home_ID=1

    SELECT
        /* =========================================
           Home Capacity
           ========================================= */
        ISNULL(h.Home_Capacity, 0) AS HomeCapacity,


        /* =========================================
           Total Active Beneficiaries
           ========================================= */
        ISNULL(b.BeneficiaryRegistered, 0)
            AS BeneficiaryRegistered,


        /* =========================================
           FRS Registered Beneficiaries
           ========================================= */
        ISNULL(b.FRSRegistered, 0)
            AS BeneficiaryFRSRegistered,


        /* =========================================
           Male Beneficiaries
           ========================================= */
        ISNULL(b.MaleCount, 0)
            AS Male,


        /* =========================================
           Female Beneficiaries
           ========================================= */
        ISNULL(b.FemaleCount, 0)
            AS Female,


        /* =========================================
           Today's Total Attendance
           ========================================= */
        ISNULL(a.TotalAttendance, 0)
            AS Attendance,


        /* =========================================
           Today's Morning Attendance
           ========================================= */
        ISNULL(a.MorningAttendance, 0)
            AS MorningAttendance,


        /* =========================================
           Today's Evening Attendance
           ========================================= */
        ISNULL(a.EveningAttendance, 0)
            AS EveningAttendance


    FROM [gmc].[tbl_Homes] h


    /* =============================================
       Beneficiary Counts
       ============================================= */

    LEFT JOIN
    (
        SELECT
            Home_ID,

            COUNT(*) AS BeneficiaryRegistered,

            COUNT
            (
                CASE
                    WHEN FRSStatus=1                       
                    THEN 1
                END
            ) AS FRSRegistered,

            COUNT
            (
                CASE
                    WHEN Gender = 'Male'
                    THEN 1
                END
            ) AS MaleCount,

            COUNT
            (
                CASE
                    WHEN Gender = 'Female'
                    THEN 1
                END
            ) AS FemaleCount

        FROM [gmc].[tbl_BeneficiaryRegistration]

        WHERE Is_Active = 1

        GROUP BY Home_ID

    ) b
        ON b.Home_ID = h.Home_ID


    /* =============================================
       Today's Attendance
       ============================================= */

    LEFT JOIN
    (
        SELECT
            Home_ID,

            COUNT(*) AS TotalAttendance,

            COUNT
            (
                CASE
                    WHEN AttendanceType = 'M'
                    THEN 1
                END
            ) AS MorningAttendance,

            COUNT
            (
                CASE
                    WHEN AttendanceType = 'E'
                    THEN 1
                END
            ) AS EveningAttendance

        FROM [gmc].[tbl_BeneficiaryAttendance]

        WHERE AttendanceDate = CAST(GETDATE() AS DATE)
          AND Is_Active = 1

        GROUP BY Home_ID

    ) a
        ON a.Home_ID = h.Home_ID


    /* =============================================
       Home
       ============================================= */

    WHERE h.Home_ID = @Home_ID


END;
GO

IF OBJECT_ID(N'[gmc].[USP_Get_MasterList]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_MasterList] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_MasterList]

@Flag varchar(100)=null,
@Home_ID int=null

AS
BEGIN
IF @Flag='Get_HomeTypes'
BEGIN

select WelfareHomeType_ID,WelfareHomeType_Name from [gmc].[tbl_WelfareHomeTypes]

END

IF @Flag='Get_HomeCategory'
BEGIN
  select HomeCategory_ID,HomeCategory_Name from [gmc].[tbl_HomeCategory]
END
IF @Flag='Get_Benf_FRS_Dtls'
BEGIN
  select * from [gmc].[tbl_BeneficiaryRegistration] with ( nolock)  where Home_ID=@Home_ID
END 

--IF @Flag=''
--BEGIN

--END

END
GO

IF OBJECT_ID(N'[gmc].[USP_Get_WelfareHomeDetails]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Get_WelfareHomeDetails] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Get_WelfareHomeDetails]
(
    @Home_ID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        h.Home_ID,h.Home_Name,-- Home Type
        wt.WelfareHomeType_ID,
        wt.WelfareHomeType_Name,

        -- Home Category
        hc.HomeCategory_ID,
        hc.HomeCategory_Name,

        h.Registration_Number,
        h.Registration_Date,
        h.Establishment_Date,
        h.Current_Status,
        h.Managing_Organization,
        h.Year_Established,
        h.Home_Capacity,
        h.Current_Occupancy,

        -- Total Active Beneficiaries
        ISNULL(b.Total_Beneficiary, 0) AS Tot_Beneficiary,

        -- Today's Morning Attendance
        ISNULL(a.Morning_Attendance, 0) AS Morning_Attendance,

        -- Today's Evening Attendance
        ISNULL(a.Evening_Attendance, 0) AS Evening_Attendance,

        -- Today's Total Attendance
        ISNULL(a.Total_Attendance, 0) AS Total_Attendance,

        h.CreatedOn,
        h.ModifiedOn

    FROM [gmc].[tbl_Homes] h

    INNER JOIN [gmc].[tbl_WelfareHomeTypes] wt
        ON wt.WelfareHomeType_ID = h.WelfareHomeType_ID

    INNER JOIN [gmc].[tbl_HomeCategory] hc
        ON hc.HomeCategory_ID = h.HomeCategory_ID

    /* =========================================
       Total Beneficiary Count
       ========================================= */

    LEFT JOIN
    (
        SELECT
            Home_ID,
            COUNT(*) AS Total_Beneficiary
        FROM [gmc].[tbl_BeneficiaryRegistration]
        WHERE Is_Active = 1
        GROUP BY Home_ID
    ) b
        ON b.Home_ID = h.Home_ID


    /* =========================================
       Today's Attendance
       ========================================= */

    LEFT JOIN
    (
        SELECT
            Home_ID,

            COUNT(CASE
                WHEN AttendanceType = 'M'
                THEN 1
            END) AS Morning_Attendance,

            COUNT(CASE
                WHEN AttendanceType = 'E'
                THEN 1
            END) AS Evening_Attendance,

            COUNT(*) AS Total_Attendance

        FROM [gmc].[tbl_BeneficiaryAttendance]

        WHERE AttendanceDate = CAST(GETDATE() AS DATE)
          AND Is_Active = 1

        GROUP BY Home_ID

    ) a
        ON a.Home_ID = h.Home_ID


    WHERE h.Home_ID = @Home_ID
      
END;
GO

IF OBJECT_ID(N'[gmc].[usp_GetMainGrievance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[usp_GetMainGrievance] AS RETURN 0;');
GO

/*
    Procedure Name: usp_GetMainGrievance
    Author: SRAV's
    Created On: 2024-12-07
    Description: Fetches MainGrievance Masters.
 
*/


ALTER PROCEDURE [gmc].[usp_GetMainGrievance]
AS
BEGIN

 SET NOCOUNT ON;


 select MainGrievanceID,MainGrievanceName,SectionID from [gmc].[tblMainGrievance]



END
GO

IF OBJECT_ID(N'[gmc].[usp_GetSubGrievance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[usp_GetSubGrievance] AS RETURN 0;');
GO

/*
    Procedure Name: usp_GetMainGrievance
    Author: SRAV's
    Created On: 2024-12-07
    Description: Fetches MainGrievance Masters.
 
*/


ALTER PROCEDURE [gmc].[usp_GetSubGrievance]
@MainGrievanceID int=null
AS
BEGIN

 SET NOCOUNT ON;


 select SubGrievanceID,SubGrievanceName,MainGrievanceID from [gmc].[TblSubGrievance] where MainGrievanceID=@MainGrievanceID
 



END
GO

IF OBJECT_ID(N'[gmc].[USP_IU_Approve_UserGrievance_By_MidLevel_EMP]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_Approve_UserGrievance_By_MidLevel_EMP] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_Approve_UserGrievance_By_MidLevel_EMP]
@UserGrievanceID int=null,
@ApprovedByEMPID int=null,
@EMPGrievanceStatus int=null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[TblUserGrievance] where UserGrievanceID=@UserGrievanceID and GrievanceStatus=2)
BEGIN
    update [gmc].[TblUserGrievance] set GrievanceStatus=3,ApprovedBy= @ApprovedByEMPID ,ApprovedStatus=1,ApprovedOn=getdate() where UserGrievanceID=@UserGrievanceID  and GrievanceStatus=2
	set @Status='000'
END
ELSE
BEGIN
    set @Status='111'

END


--insert into [gmc].[TblUserGrievance] (UserID,Username ,MainGrievanceID,
--SubGrievanceID,Description,ImageBase64,Image1 ,GrievanceStatus,
--CreatedOn,Langitude,Latitude
--)values(@UserID,@Username ,@MainGrievanceID,@SubGrievanceID,@Description,
--@ImageBase64,@Image1 ,1,getdate(),@Langitude,@Latitude)
--set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END


select * from [gmc].[TblUserGrievance]
GO

IF OBJECT_ID(N'[gmc].[USP_IU_CnD_Assignment]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_CnD_Assignment] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_CnD_Assignment]
@CnDBookingID int null,
@EMPID int null,
@TruckDriverID int  null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[tblCnDBookingDtls] where CnDBookingID=@CnDBookingID )
BEGIN
    
	   insert into [gmc].[TblCnDAssignments](CnDBookingID ,EMPID ,TruckDriverID) values(@CnDBookingID ,@EMPID ,@TruckDriverID)
	   update [gmc].[tblCnDBookingDtls] set CnDBookingStatus=2 where CnDBookingID=@CnDBookingID
	   update [gmc].[TblCnDTruckDriverDtls] set AvailabilityStatus=0,ModifiedOn=GETDATE() where TruckDriverID=@TruckDriverID
	
	set @Status='000'
END
ELSE
BEGIN
    set @Status='111'

END


SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END


--EXEC [gmc].[USP_IU_CnD_Assignment] @CnDBookingID=1,@EMPID=10000,@TruckDriverID =1
GO

IF OBJECT_ID(N'[gmc].[USP_IU_CnD_Assignment_UpdateByTruckDriver]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_CnD_Assignment_UpdateByTruckDriver] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_CnD_Assignment_UpdateByTruckDriver]
@CndAssignmentID int null,
@CnDBookingID int null,
@TruckDriverID int  null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[TblCnDAssignments] where CndAssignmentID=@CndAssignmentID and CndBookingID=@CnDBookingID and CnDStatus='Assigned')
BEGIN    
	   update [gmc].[TblCnDAssignments] set CompletionDate=Getdate(),CndStatus='Completed' where CndAssignmentID=@CndAssignmentID and CndBookingID=@CnDBookingID
	   update [gmc].[TblCnDTruckDriverDtls] set AvailabilityStatus=1,ModifiedOn=GETDATE() where TruckDriverID=@TruckDriverID
	   update [gmc].[tblCnDBookingDtls] set CnDBookingStatus=3 where CnDBookingID=@CnDBookingID
	   set @Status='000'	
END
ELSE
BEGIN
    set @Status='111'

END

SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END



END

--select * from [gmc].[TblCnDAssignments]
--select * from [gmc].[tblCnDBookingDtls]
--select * from [gmc].[TblCnDTruckDriverDtls]


--CnDBookingStatus==> 1,Active, 2,Assigned to Truck, 3=Completed
GO

IF OBJECT_ID(N'[gmc].[USP_IU_ELRS_FilledDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_ELRS_FilledDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_ELRS_FilledDtls]
@ELSRID int=null,
@EMPID int =null,
@ELSRTimeSlatID int=null,
@FilledDate varchar(10)=null,
@Noof_KL_Filled varchar(20)=null,
@Remarks varchar(500)=null

--ELSRID int,FilledDate datetime,ELSRTimeSlatID int,Noof_KL_Filled varchar(20),Remaks varchar(100),AE_ID int,CreatedOn Datetime
--RCTestingID int identity(1,1) primary key,ELSRID int,ImageBase64 nvarchar(max),Description nvarchar(500),CreatedBy int,CreateOn Datetime
AS

BEGIN

BEGIN TRANSACTION

Declare @Date Date = convert(date,getdate(),103);
DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[tbl_ELSR_FilledDtls] where ELSRID=@ELSRID and FilledDate=@Date and  ELSRTimeSlatID=@ELSRTimeSlatID)
BEGIN
    
	   set @Status='111'
END
ELSE
BEGIN
    
	insert into [gmc].[tbl_ELSR_FilledDtls](ELSRID,FilledDate,ELSRTimeSlatID,Noof_KL_Filled,Remaks,AE_ID,CreatedOn) values
	                                  (@ELSRID,@Date,@ELSRTimeSlatID,@Noof_KL_Filled,@Remarks,@EMPID ,GETDATE())

	
	set @Status='000'

END


SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END
GO

IF OBJECT_ID(N'[gmc].[USP_IU_RCTesting]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_RCTesting] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_RCTesting]
@EMPID int =null,
@ELSRID int=null,
@ImageBase64 nvarchar(max)=null,
@Description nvarchar(500)=null,
@Result varchar(300)=null


--RCTestingID int identity(1,1) primary key,ELSRID int,ImageBase64 nvarchar(max),Description nvarchar(500),CreatedBy int,CreateOn Datetime
AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;


--BEGIN
    
	   insert into [gmc].[tbl_RCTesting](ELSRID,ImageBase64,Description,CreatedBy,CreateOn,Result) values(@ELSRID,@ImageBase64,@Description,@EMPID ,GETDATE(),@Result)

	
	set @Status='000'
--END
--ELSE
----BEGIN
----    set @Status='111'

----END


SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END
GO

IF OBJECT_ID(N'[gmc].[USP_IU_UserGrievance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_IU_UserGrievance] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_IU_UserGrievance]
@UserGrievanceID int=null,
@UpdatedByEMPID int=null,
@EMPGrievanceStatus int=null,
@EMPImageBase64 nvarchar(max) = null,
@EMPDescription nvarchar(500)=null

AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

IF EXISTS(select 1 from [gmc].[TblUserGrievance] where UserGrievanceID=@UserGrievanceID and GrievanceStatus=1)
BEGIN
    update [gmc].[TblUserGrievance] set GrievanceStatus=2,UpdatedByEMPID= @UpdatedByEMPID ,EMPGrievanceStatus=1,EMPImageBase64=@EMPImageBase64,EMPUpdatedDate=getdate(),EMPDescription=@EMPDescription where UserGrievanceID=@UserGrievanceID  and GrievanceStatus=1
	set @Status='000'
END
ELSE
BEGIN
    set @Status='111'

END


--insert into [gmc].[TblUserGrievance] (UserID,Username ,MainGrievanceID,
--SubGrievanceID,Description,ImageBase64,Image1 ,GrievanceStatus,
--CreatedOn,Langitude,Latitude
--)values(@UserID,@Username ,@MainGrievanceID,@SubGrievanceID,@Description,
--@ImageBase64,@Image1 ,1,getdate(),@Langitude,@Latitude)
--set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END
GO

IF OBJECT_ID(N'[gmc].[USP_Maters_List]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Maters_List] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Maters_List]
@Flag varchar(100)=null,
@Usename varchar(10)=null,
@WardID int=null

AS


set nocount on;

BEGIN

Declare @appuserid int



IF @Flag='GetGarbageCollectionTypes'
BEGIN
	select GarbageCollectionTypeID,GarbageCollectionTypeName  from [gmc].[tbl_GarbageCollectionTypes]

END
IF @Flag='GetEventMasters'
BEGIN

select * from [gmc].[tbl_EventTypes]
select * from [gmc].[tbl_EventTimes]
select * from [gmc].[tbl_EventParticipationMembers]
END

IF @Flag='GetAppUserDtlsByUserName'
BEGIN
  
   select @appuserid=ID from [gmc].[TblAppUser] a where UserName=@Usename
   select a.* from [gmc].[TblAppUser] a where UserName=@Usename
   ;with Addr as(
select 
b.AddressID,b.UserID,a.FullName,b.HNo,b.Building_Name,b.StreetOrLineNum,b.Area_Name,b.mobile
,b.LandMark,b.City,b.nickname,b.CreatedOn,b.ModifiedBy,b.Langitude
,b.Latitude
,'HNo:'+isnull(b.HNo,'')+', Building_Name:'+isnull(b.Building_Name,'') +' ,StreetOrLineNum:'+isnull(b.StreetOrLineNum,'')+',AreaName:'+isnull(b.Area_Name,'')+',LandMark:'+isnull(b.LandMark,'')+',City:'+isnull(b.City,'')+',nickname:'+isnull(b.nickname,'')+',PinCode:'+b.pincode as 'Address'
,b.SecretariatID
,s.SecretariatName
,b.WardID
,w.WardName,  RANK() OVER ( ORDER BY  b.CreatedOn Desc) as 'SNO'
 		
from [gmc].[tblAddress] b with(nolock) 
inner join [gmc].[TblAppUser] a with(nolock) on a.ID=b.UserID 
inner join [gmc].[TblSecretariat] s  with(nolock) on s.SecretariatID=b.SecretariatID 
inner join [gmc].[TblWards] w with(nolock) on w.WardID=b.WardID and w.WardID=s.WardID
where a.ID=@appuserid )


select AddressID,Address from Addr order by AddressID



END

IF @Flag='GetWards'
BEGIN

  select WardID,WardName from [gmc].[TblWards] order by WardID

END


IF @Flag='GetSecreteriateByWard'
BEGIN

  select SecretariatID,SecretariatName from [gmc].[TblSecretariat] where WardID=@WardID

END

IF @Flag='getMainGrievance'
BEGIN
  select MainGrievanceID,MainGrievanceName,SectionID from [gmc].[tblMainGrievance] where MainGrievanceID<>8

END
IF @Flag='getGrievanceReceivingCenters'
BEGIN
  select CenterID,CenterName from [gmc].[tbl_GrievanceReceivingCenter]
  END

  IF @Flag='getGrievanceReceivingModes'
BEGIN
     select ReceivingModeID,ReceivingMode from [gmc].[tbl_GrievanceRecievingModes]
  END
  
  IF @Flag='getEmpTypes'
BEGIN
     select EmpTypeID,EmpType
  FROM SERP.tbl_EMP_Types
  END
END
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_CnD_Consolidation]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_CnD_Consolidation] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_CnD_Consolidation]
@Flag varchar(50)=null,
@FromDate datetime=null,
@ToDate datetime=null,
@RMonth int=null,
@RYear int=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null,
@valueFlag varchar(50)=null

AS
BEGIN

set nocount on;

Declare @CurrentDate datetime=getdate();

IF @Flag='GetDataByMonthYear'
BEGIN
   
  ;with CTE AS(select a.CnDBookingID,a.CnDBookingStatus,a.BookingDate from [gmc].[tblCnDBookingDtls] a with (nolock) 
                where month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear )
  
  select
  (select count(CnDBookingID) from CTE with (nolock) ) as 'Sec1',
  --Dispose 
  (select count(CnDBookingID) from CTE with (nolock) where CnDBookingStatus=3 ) as 'Dispose_Sec1',
  --Pending  
  (select count(CnDBookingID) from CTE with (nolock) where CnDBookingStatus in(1,2)) as 'Pending_Sec1'



END

IF @Flag='GetDataByYear'
BEGIN
   
  ;with CTE AS(select a.CnDBookingID,a.CnDBookingStatus,a.BookingDate from [gmc].[tblCnDBookingDtls] a with (nolock)  
                where  year(a.BookingDate)=@RYear )  
  select (select count(CnDBookingID) from CTE with (nolock) ) as 'Sec1',  
  --Dispose 
  (select count(CnDBookingID) from CTE with (nolock) where CnDBookingStatus=3 ) as 'Dispose_Sec1',
  --Pending
  (select count(CnDBookingID) from CTE with (nolock) where CnDBookingStatus in(1,2)  ) as 'Pending_Sec1'
  
END

IF @Flag='GetDataByTwoDates'
BEGIN
   
  ;with CTE AS( select a.CnDBookingID,a.CnDBookingStatus,a.BookingDate from [gmc].[tblCnDBookingDtls] a with (nolock) 
               where  CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) )
  
  select
  (select count(CnDBookingID) from CTE with (nolock)) as 'Sec1',  
  --Dispose Befeore Due Dates
  (select count(CnDBookingID) from CTE with (nolock) where CnDBookingStatus=3 ) as 'Dispose_Sec1',  
    --Pending Befeore Due Dates
  (select count(CnDBookingID) from CTE with (nolock)  where CnDBookingStatus in(1,2) ) as 'Pending_Sec1'
  

END

IF @valueFlag='Sec_Tot'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				order by a.CnDBookingID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				order by a.CnDBookingID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				order by a.CnDBookingID
			  END
END
IF @valueFlag='Sec_D'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.CnDBookingStatus=3
				order by a.CnDBookingID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.CnDBookingStatus=3
				order by a.CnDBookingID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.CnDBookingStatus=3
				order by a.CnDBookingID
			  END
END
IF @valueFlag='Sec_P'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.CnDBookingStatus in(1,2)
				order by a.CnDBookingID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.CnDBookingStatus in(1,2)
				order by a.CnDBookingID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.CnDBookingID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				wc.WasteCategoryName,wt.WasteTypeName,
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'CnDBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'CnDBookingClosedDate'
				from [gmc].[tblCnDBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblWasteCategory] wc on wc.WasteCategoryID=a.WasteCategoryID
				left join [gmc].[TblWasteType] wt on wt.WasteTypeID=a.WasteTypeID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.CnDBookingStatus in(1,2)
				order by a.CnDBookingID
			  END
END

END

  --exec  [gmc].[USP_Rpt_CnD_Consolidation] @Flag='GetDataByMonthYear',@RMonth=3,@RYear=2025
  --exec  [gmc].[USP_Rpt_CnD_Consolidation] @Flag='GetDataByYear',@RYear=2025
  --exec  [gmc].[USP_Rpt_CnD_Consolidation] @Flag='GetDataByTwoDates',@FromDate='2025-05-01 10:13:29.003',@ToDate='2025-05-18 10:13:29.003'
  --exec  [gmc].[USP_Rpt_CnD_Consolidation]  @Flag='onlyyear',@valueFlag='Sec_Tot',@RYear=2024

--select * from [gmc].[tblCnDBookingDtls]

--select UserGrievanceID,CreatedOn from [gmc].[TblUserGrievance] order by CreatedOn
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_DB_Mobile_Grievance_DataForEmpUser]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_DB_Mobile_Grievance_DataForEmpUser] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_DB_Mobile_Grievance_DataForEmpUser]
@EMPID int=null,
@Flag varchar(500)=null

AS
BEGIN

--**************************************
--Declare @EMPID int=10299
--Declare @Flag varchar(500)='GetGrievanceForEMP'
--EXEC [gmc].[USP_GET_Grievance_DataForEmpUser] @EMPID =10293,@Flag ='USP_Rpt_DB_Mobile_Grievance_DataForEmpUser'
--**************************************

set nocount on;

Declare @SectionID int
Declare @SecretariatID int
Declare @Role varchar(20)
Declare @UserType int

select @SectionID=Section,@Role=Role,@UserType=UserType  from [gmc].[TblEMPUser] with(nolock) where EMPID=@EMPID and IsActive=1
print @SectionID
print @Role
print @UserType


IF @Flag='GetGrievanceStatusCountForEMPAccessLevel'
BEGIN

   IF @Role='SectionAdmin' 
   BEGIN
			  --select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		   --   a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  --case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  --cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  --a.EMPImageBase64,a.EMPUpdatedDate
		   --   from [gmc].[TblUserGrievance] a 
		   --   left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		   --   left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  --where a.SectionID=@SectionID
		   --   order by a.UserID, a.GrievanceStatus asc
		  
			select ( select count(UserGrievanceID) from [gmc].[TblUserGrievance] with (nolock) where SectionID=@SectionID) as 'TotalGrievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance]  with (nolock) where GrievanceStatus=1 and  SectionID=@SectionID) as 'Total_Active_Grievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance]  with (nolock) where GrievanceStatus=2 and  SectionID=@SectionID) as 'TotalInProcess',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance]  with (nolock) where GrievanceStatus=3 and  SectionID=@SectionID) as 'Total_Completed_Grievances'
				      
		


	END
	ELSE IF  @Role='SubAdmin'
	BEGIN
	    -- select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		   --   a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  --case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  --cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  --a.EMPImageBase64,a.EMPUpdatedDate
		   --   from [gmc].[TblUserGrievance] a 
		   --   left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		   --   left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  --inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  --where a.SectionID=@SectionID and m.EMPID=@EMPID
		   --   order by a.UserID, a.GrievanceStatus asc

		   	select ( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID where SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalGrievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=1 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Active_Grievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=2 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalInProcess',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=3 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Completed_Grievances'
				      
		


	END
	ELSE IF  @Role='MidLevel'
	BEGIN
	    -- select a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		   --   a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  --case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'InProcess' when a.GrievanceStatus=3 then 'Completed' else '' end as 'GrievanceStatus',a.Latitude,a.Langitude,
			  --cast(day(a.CreatedOn) as varchar) as 'Day',cast(FORMAT(a.CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.CreatedOn) as varchar),2) as 'MonthYear',
			  --a.EMPImageBase64,a.EMPUpdatedDate
		   --   from [gmc].[TblUserGrievance] a 
		   --   left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		   --   left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
			  --inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID
			  --where a.SectionID=@SectionID and m.EMPID=@EMPID
		   --   order by a.UserID, a.GrievanceStatus asc
		   	select ( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID where SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalGrievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=1 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Active_Grievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=2 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalInProcess',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=3 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Completed_Grievances'
				      
		
	END
	ELSE IF  @Role='EndLevel'
	BEGIN
	    	select ( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID where SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalGrievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=1 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Active_Grievances',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=2 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'TotalInProcess',
			( select count(UserGrievanceID) from [gmc].[TblUserGrievance] a with (nolock) inner join [gmc].[tblEMPMappingSecretariat] m on m.SecretariatID=a.SecretariatID  where GrievanceStatus=3 and  SectionID=@SectionID and m.EMPID=@EMPID) as 'Total_Completed_Grievances'
				      
		
	END
END



END


--EXEC [gmc].[USP_EMP_Master_List] @Flag='GetGrievance_StatusCountForEMP'

--select * from [gmc].[TblUserGrievance] where SectionID=4
--select * from [gmc].[tblEMPMappingSecretariat]
--select distinct Role from [gmc].[TblEMPUser]
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_DB_Portal]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_DB_Portal] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_DB_Portal]
@EMPID int=null,
@Flag varchar(500)=null,
@Rmonth int=null,
@Ryear int=null

AS
BEGIN

--**************************************
--Declare @EMPID int=10299
--Declare @Flag varchar(500)='GetGrievanceForEMP',
--EXEC [gmc].[USP_Rpt_DB_Portal] @Rmonth=4 , @Ryear=2025
--**************************************

set nocount on;

--Declare @SectionID int
--Declare @SecretariatID int
--Declare @Role varchar(20)
--Declare @UserType int

--select @SectionID=Section,@Role=Role,@UserType=UserType  from [gmc].[TblEMPUser] with(nolock) where EMPID=@EMPID and IsActive=1
--print @SectionID
--print @Role
--print @UserType

	select 
	      (select count(*) from [gmc].[TblEMPUser] with(nolock) where IsActive=1 and EMPID not in(10000,10001)) as 'Tot_Staff',
		  (select count(*) from [gmc].[Tbl_Secretariat] with(nolock) ) as 'Tot_Secretariat',
	      (select count(*) from [gmc].[TblWards]) as 'Tot_Wards',
		  '9.6L' as 'Tot_Popuation',
		  (select count(UserGrievanceID) from [gmc].[TblUserGrievance] with (nolock) where month(createdOn)=@Rmonth and year(CreatedOn)=@Ryear) as 'TotalGrievances',
		  (select count(UserGrievanceID) from [gmc].[TblUserGrievance]  with (nolock) where GrievanceStatus in(1,2) and month(createdOn)=@Rmonth and year(CreatedOn)=@Ryear) as 'Tot_Grievanc_Pending',
	      (select count(UserGrievanceID) from [gmc].[TblUserGrievance]  with (nolock) where GrievanceStatus=3 and month(createdOn)=@Rmonth and year(CreatedOn)=@Ryear) as 'Tot_Grievanc_Disposed',
		  (select count(WaterTankerID) from [gmc].[tblWaterTankerBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear ) as 'Tot_WT',
		  (select count(WaterTankerID) from [gmc].[tblWaterTankerBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and TankerStatus=1) as 'Tot_WT_Pending',
		  (select count(WaterTankerID) from [gmc].[tblWaterTankerBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and TankerStatus=2) as 'Tot_WT_Disposed',
		  (select count(CnDBookingID) from [gmc].[tblCnDBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear )  as 'Tot_CnD',
		  (select count(CnDBookingID) from [gmc].[tblCnDBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and CnDBookingStatus in(1,2) )  as 'Tot_CnD_Pending',
		  (select count(CnDBookingID) from [gmc].[tblCnDBookingDtls] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and CnDBookingStatus =3 )  as 'Tot_CnD_Disposed',
		  (select count(EGCollectionID) from [gmc].[tbl_EventGarbageCollection] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear )  as 'Tot_EG',
		  (select count(EGCollectionID) from [gmc].[tbl_EventGarbageCollection] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and EGCollectionStatus in(1,2) )  as 'TotEG_Pending',
		  (select count(EGCollectionID) from [gmc].[tbl_EventGarbageCollection] with (nolock) where MONTH(BookingDate)=@Rmonth and YEAR(BookingDate)=@Ryear and EGCollectionStatus =3 )  as 'Tot_EG_Disposed'
		  
		  
		     --select * from [gmc].[tbl_EventGarbageCollection] 
		  -- select * from [gmc].[tblCnDBookingDtls]
		   --select * from [gmc].[tblWaterTankerBookingDtls] order by CreatedOn asc
		  -- select * from [gmc].[TblUserGrievance] order by CreatedOn
				      

		 



END
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_GarbageCollection_Consolidation]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_GarbageCollection_Consolidation] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_GarbageCollection_Consolidation]
@Flag varchar(50)=null,
@FromDate datetime=null,
@ToDate datetime=null,
@RMonth int=null,
@RYear int=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null,
@valueFlag varchar(50)=null

AS
BEGIN

set nocount on;

Declare @CurrentDate datetime=getdate();

IF @Flag='GetDataByMonthYear'
BEGIN
   
  ;with CTE AS(select a.EGCollectionID,a.EGCollectionStatus,a.BookingDate from [gmc].[tbl_EventGarbageCollection] a with (nolock) 
                where month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear )
  
  select
  (select count(EGCollectionID) from CTE with (nolock) ) as 'Sec1',
  --Dispose 
  (select count(EGCollectionID) from CTE with (nolock) where EGCollectionStatus=3 ) as 'Dispose_Sec1',
  --Pending  
  (select count(EGCollectionID) from CTE with (nolock) where EGCollectionStatus in(1,2)) as 'Pending_Sec1'



END

IF @Flag='GetDataByYear'
BEGIN
   
  ;with CTE AS(select a.EGCollectionID,a.EGCollectionStatus,a.BookingDate from [gmc].[tbl_EventGarbageCollection] a with (nolock)  
                where  year(a.BookingDate)=@RYear )  
  select (select count(EGCollectionID) from CTE with (nolock) ) as 'Sec1',  
  --Dispose 
  (select count(EGCollectionID) from CTE with (nolock) where EGCollectionStatus=3 ) as 'Dispose_Sec1',
  --Pending
  (select count(EGCollectionID) from CTE with (nolock) where EGCollectionStatus in(1,2)  ) as 'Pending_Sec1'
  
END

IF @Flag='GetDataByTwoDates'
BEGIN
   
  ;with CTE AS( select a.EGCollectionID,a.EGCollectionStatus,a.BookingDate from [gmc].[tbl_EventGarbageCollection] a with (nolock) 
               where  CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) )
  
  select
  (select count(EGCollectionID) from CTE with (nolock)) as 'Sec1',  
  --Dispose Befeore Due Dates
  (select count(EGCollectionID) from CTE with (nolock) where EGCollectionStatus=3 ) as 'Dispose_Sec1',  
    --Pending Befeore Due Dates
  (select count(EGCollectionID) from CTE with (nolock)  where EGCollectionStatus in(1,2) ) as 'Pending_Sec1'
  

END

IF @valueFlag='Sec_Tot'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				order by a.EGCollectionID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				order by a.EGCollectionID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
		
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				order by a.EGCollectionID
			  END
END
IF @valueFlag='Sec_D'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.EGCollectionStatus=3
				order by a.EGCollectionID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.EGCollectionStatus=3
				order by a.EGCollectionID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.EGCollectionStatus=3
				order by a.EGCollectionID
			  END
END
IF @valueFlag='Sec_P'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.EGCollectionStatus in(1,2)
				order by a.EGCollectionID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.EGCollectionStatus in(1,2)
				order by a.EGCollectionID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.EGCollectionID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalPrice,
				convert(varchar(10),a.BookingDate,103) as 'GarbageCollectionReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'GarbageCollectionClosedDate'
				from [gmc].[tbl_EventGarbageCollection] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.PaymentConfirmedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.EGCollectionStatus in(1,2)
				order by a.EGCollectionID
			  END
END

END

  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByMonthYear',@RMonth=3,@RYear=2025
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByYear',@RYear=2025
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByTwoDates',@FromDate='2025-05-01 10:13:29.003',@ToDate='2025-05-18 10:13:29.003'
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation]  @Flag='onlyyear',@valueFlag='Sec_Tot',@RYear=2024

--select * from [gmc].[tbl_EventGarbageCollection]

--select UserGrievanceID,CreatedOn from [gmc].[TblUserGrievance] order by CreatedOn
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_Grievance_Consolidation]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_Grievance_Consolidation] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_Grievance_Consolidation]
@Flag varchar(50)=null,
@FromDate datetime=null,
@ToDate datetime=null,
@RMonth int=null,
@RYear int=null,
@FD varchar(10)=null,
@TD varchar(10)=null

AS
BEGIN

set nocount on;

Declare @CurrentDate datetime=getdate();

IF @Flag='GetDataByMonthYear'
BEGIN
   
  ;with CTE AS(select a.UserGrievanceID,a.MainGrievanceID,a.SubGrievanceID,a.GrievanceStatus,a.CreatedOn,a.ApprovedOn,a.SectionID,b.DueHours from [gmc].[TblUserGrievance] a with (nolock) 
left join [gmc].[TblSubGrievance] b on a.SubGrievanceID=b.SubGrievanceID
where month(createdOn)=@RMonth and year(CreatedOn)=@RYear )
  
  select
  (select count(UserGrievanceID) from CTE with (nolock) where  SectionID=1) as 'Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where  SectionID=2) as 'Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where  SectionID=3) as 'Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where  SectionID=4) as 'Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where  SectionID=5) as 'Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where  MainGrievanceID=8) as 'Sec_Others',
  --Dispose Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= 48)  as 'DisposeBeforeDueDate_Sec_Others',
    --Dispose After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > 48)  as 'DisposeAfterDueDate_Sec_Others',
    --Pending Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= 48)       as 'PendingBeforeDueDate_Sec_Others',
      --Pending After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > 48)       as 'PendingAfterDueDate_Sec_Others'


END

IF @Flag='GetDataByYear'
BEGIN
   
  ;with CTE AS(select a.UserGrievanceID,a.MainGrievanceID,a.SubGrievanceID,a.GrievanceStatus,a.CreatedOn,a.ApprovedOn,a.SectionID,b.DueHours from [gmc].[TblUserGrievance] a with (nolock) 
left join [gmc].[TblSubGrievance] b on a.SubGrievanceID=b.SubGrievanceID
where  year(CreatedOn)=@RYear )
  
  select
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1) as 'Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2) as 'Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3) as 'Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4) as 'Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5) as 'Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8) as 'Sec_Others',
  --Dispose Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= 48)  as 'DisposeBeforeDueDate_Sec_Others',
    --Dispose After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > 48)  as 'DisposeAfterDueDate_Sec_Others',
    --Pending Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= 48)       as 'PendingBeforeDueDate_Sec_Others',
      --Pending After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > 48)       as 'PendingAfterDueDate_Sec_Others'


END

IF @Flag='GetDataByTwoDates'
BEGIN
   
  ;with CTE AS(select a.UserGrievanceID,a.MainGrievanceID,a.SubGrievanceID,a.GrievanceStatus,a.CreatedOn,a.ApprovedOn,a.SectionID,b.DueHours from [gmc].[TblUserGrievance] a with (nolock) 
left join [gmc].[TblSubGrievance] b on a.SubGrievanceID=b.SubGrievanceID
where  CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) )
  
  select
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1) as 'Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2) as 'Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3) as 'Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4) as 'Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5) as 'Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8) as 'Sec_Others',
  --Dispose Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= 48)  as 'DisposeBeforeDueDate_Sec_Others',
    --Dispose After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > 48)  as 'DisposeAfterDueDate_Sec_Others',
    --Pending Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= 48)       as 'PendingBeforeDueDate_Sec_Others',
      --Pending After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > 48)       as 'PendingAfterDueDate_Sec_Others'


END

IF @Flag='GetDataByTwoDates_For_Amaravathi'
BEGIN

	 ;with CTE AS(select a.UserGrievanceID,a.MainGrievanceID,a.SubGrievanceID,a.GrievanceStatus,a.CreatedOn,a.ApprovedOn,a.SectionID,b.DueHours from [gmc].[TblUserGrievance] a with (nolock) 
left join [gmc].[TblSubGrievance] b on a.SubGrievanceID=b.SubGrievanceID
where  convert(date,a.CreatedOn ,103) between convert(date, @FD,103) and convert(date,@TD ,103) )
  

  select
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1) as 'Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2) as 'Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3) as 'Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4) as 'Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5) as 'Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8) as 'Sec_Others',
  --Dispose Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= DueHours) as 'DisposeBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) <= 48)  as 'DisposeBeforeDueDate_Sec_Others',
    --Dispose After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > DueHours) as 'DisposeAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus=3 and DATEDIFF(HOUR, CreatedOn, ApprovedOn) > 48)  as 'DisposeAfterDueDate_Sec_Others',
    --Pending Befeore Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= DueHours) as 'PendingBeforeDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) <= 48)       as 'PendingBeforeDueDate_Sec_Others',
      --Pending After Due Dates
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=1 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec1',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=2 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec2',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=3 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec3',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=4 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec4',
  (select count(UserGrievanceID) from CTE with (nolock) where SectionID=5 and  GrievanceStatus in(1,2)        and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > DueHours) as 'PendingAfterDueDate_Sec5',
  (select count(UserGrievanceID) from CTE with (nolock) where MainGrievanceID=8 and GrievanceStatus in(1,2)   and DATEDIFF(HOUR, CreatedOn, @CurrentDate) > 48)       as 'PendingAfterDueDate_Sec_Others'

  into #tmp
  Declare @tmp as table(ID int,Section varchar(50),Total_Grievance int,DisposeBeforeDueDate int,DisposeAfterDueDate int,PendingBeforeDueDate int,PendingAfterDueDate int)
  insert into @tmp values(1,'URBAN POVERTY ALLEVIATION CELL',(select Sec1 from #tmp),(select DisposeBeforeDueDate_Sec1 from #tmp),(select DisposeAfterDueDate_Sec1 from #tmp),(select PendingBeforeDueDate_Sec1 from #tmp),(select PendingAfterDueDate_Sec1 from #tmp)),
(2,'PUBLIC HEALTH SECTION',(select Sec2 from #tmp),(select DisposeBeforeDueDate_Sec2 from #tmp),(select DisposeAfterDueDate_Sec2 from #tmp),(select PendingBeforeDueDate_Sec2 from #tmp),(select PendingAfterDueDate_Sec2 from #tmp)),
(3,'REVENUE SECTION',(select Sec3 from #tmp),(select DisposeBeforeDueDate_Sec3 from #tmp),(select DisposeAfterDueDate_Sec3 from #tmp),(select PendingBeforeDueDate_Sec3 from #tmp),(select PendingAfterDueDate_Sec3 from #tmp)),
(4,'Town Planing',(select Sec4 from #tmp),(select DisposeBeforeDueDate_Sec4 from #tmp),(select DisposeAfterDueDate_Sec4 from #tmp),(select PendingBeforeDueDate_Sec4 from #tmp),(select PendingAfterDueDate_Sec4 from #tmp)),
(5,'ENGINEERING SECTION',(select Sec5 from #tmp),(select DisposeBeforeDueDate_Sec1 from #tmp),(select DisposeAfterDueDate_Sec5 from #tmp),(select PendingBeforeDueDate_Sec5 from #tmp),(select PendingAfterDueDate_Sec5 from #tmp)),
(6,'Others',(select Sec_Others from #tmp),(select DisposeBeforeDueDate_Sec_Others from #tmp),(select DisposeAfterDueDate_Sec_Others from #tmp),(select PendingBeforeDueDate_Sec_Others from #tmp),(select PendingAfterDueDate_Sec_Others from #tmp))


 select * from @tmp

  drop table #tmp
 

END



END



 --exec  [gmc].[USP_Rpt_Grievance_Consolidation] @Flag='GetDataByMonthYear',@RMonth=12,@RYear=2024
  --exec  [gmc].[USP_Rpt_Grievance_Consolidation] @Flag='GetDataByTwoDates',@FromDate='2025-05-01 10:13:29.003',@ToDate='2025-05-18 10:13:29.003'
--exec  [gmc].[USP_Rpt_Grievance_Consolidation] @Flag='GetDataByTwoDates_For_Amaravathi',@FromDate='01/07/2025',@ToDate='07/07/2025'
--select * from [gmc].[TblSections]
--select * from [gmc].[TblMainGrievance]
--select * from [gmc].[TblSubGrievance]
--select UserGrievanceID,CreatedOn from [gmc].[TblUserGrievance] order by CreatedOn
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_Grievance_List]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_Grievance_List] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_Grievance_List]
@Flag varchar(50)=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null,
@RMonth int=null,
@RYear int=null,
@valueFlag varchar(50)=null,
@SectionID int=null

AS
BEGIN

set nocount on;


--******************************************************
--EXEC [gmc].[USP_Rpt_Grievance_List] @Flag='monthyear', @valueFlag='Sec_PBDD',@RMonth=4,@RYear=2025,@SectionID=2
--Section=5&ValueFlag=Sec_Tot&fromDate=&toDate=&month=5&year=2025&F=monthyear
Declare @CurrentDate datetime=getdate();
Declare @FromDate datetime=convert(datetime,@FDate,103)
Declare @ToDate datetime=convert(datetime,@TDate,103)

IF @valueFlag='Sec_Tot'
BEGIN
        IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--c.SubGrievanceName,a.Description,
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  
				   OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and month(a.createdOn)=@RMonth and year(a.CreatedOn)=@RYear 
				order by a.UserGrievanceID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				order by a.UserGrievanceID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and  year(a.CreatedOn)=@RYear 
				order by a.UserGrievanceID asc
			  END
END

IF @valueFlag='Sec_DBDD'
BEGIN
        IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and month(a.createdOn)=@RMonth and year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus=3 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= 48)) 
				order by a.UserID, a.GrievanceStatus asc
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.GrievanceStatus=3 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and  year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus=3 
				and ( (a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) <= 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
END

IF @valueFlag='Sec_DADD'
BEGIN
        IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and month(a.createdOn)=@RMonth and year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus=3 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.GrievanceStatus=3 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and  year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus=3 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, a.ApprovedOn) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
END

IF @valueFlag='Sec_PBDD'
BEGIN
        IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and month(a.createdOn)=@RMonth and year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus in(1,2)
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.GrievanceStatus in(1,2) 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and  year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus  in(1,2) 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) <= 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
END

IF @valueFlag='Sec_PADD'
BEGIN
        IF @Flag='monthyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and month(a.createdOn)=@RMonth and year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus in(1,2)  
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) ) 
				and CAST(a.CreatedOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.GrievanceStatus in(1,2) 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.UserGrievanceID , --a.UserID,
				u.FullName as 'Name',cast(a.Username as varchar) as 'Mobile',
				b.MainGrievanceName,
				'HNo:'+isnull(ad.HNo,'')+', Building_Name:'+isnull(ad.Building_Name,'') +' ,StreetOrLineNum:'+isnull(ad.StreetOrLineNum,'')+',AreaName:'+isnull(ad.Area_Name,'')+',LandMark:'+isnull(ad.LandMark,'')+',City:'+isnull(ad.City,'')+',nickname:'+isnull(ad.nickname,'') as 'Address',
				--ConcernClerk
				(SELECT Top 1 u1.FullName FROM [gmc].[tblEMPMappingSecretariat] s inner join [gmc].[TblEMPUser] u1 on u1.EMPID=s.EMPID and s.EMPID=u1.EMPID
                     where  s.SecretariatID=ad.SecretariatID and s.Role='Endlevel' ) as 'ConcernClerk',
                --
				m.FullName as 'ApprovedBy',
				convert(varchar(10),a.CreatedOn,103) as 'ComplaintReceivedDate',
				convert(varchar(10),a.EMPUpdatedDate,103) as 'ComplaintClosedDate',a.EMPDescription
				from [gmc].[TblUserGrievance] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
				left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.ApprovedBy
				where ((a.SectionID IS NOT NULL AND a.SectionID = @SectionID)  OR (a.SectionID IS NULL AND a.MainGrievanceID = @SectionID) )
				and  year(a.CreatedOn)=@RYear 
				and a.GrievanceStatus in(1,2) 
				and ((a.SectionID IS NOT NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > c.DueHours)  OR (a.SectionID IS NULL AND DATEDIFF(HOUR, a.CreatedOn, @CurrentDate) > 48) )
				order by a.UserID, a.GrievanceStatus asc
			  END
END

END
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_RCTesting]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_RCTesting] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_RCTesting]
@Flag varchar(50)=null,
@FromDate datetime=null,
@ToDate datetime=null,
@RMonth int=null,
@RYear int=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null

AS
BEGIN

set nocount on;

Declare @CurrentDate datetime=getdate();
set @FromDate =convert(datetime,@FDate,103)
set @ToDate =convert(datetime,@TDate,103)

    

        IF @Flag='monthyear'
		BEGIN
				select  a.RCTestingID , 
					e.ELSR_Location,e.ELSR_Capacity,	e.AE_Code,
					a.Description
				
				from [gmc].[tbl_RCTesting] a 
				left join [gmc].[tbl_ELSR] e on e.ELSRID=a.ELSRID
				
				where 
				month(a.CreateOn)=@RMonth and year(a.CreateOn)=@RYear 
				
				order by a.RCTestingID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.RCTestingID , 
					e.ELSR_Location,e.ELSR_Capacity,	e.AE_Code,
					a.Description
				from [gmc].[tbl_RCTesting] a 
				left join [gmc].[tbl_ELSR] e on e.ELSRID=a.ELSRID
				where 
				CAST(a.CreateOn AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				
				order by a.RCTestingID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.RCTestingID , 
					e.ELSR_Location,e.ELSR_Capacity,	e.AE_Code,
					a.Description
				from [gmc].[tbl_RCTesting] a 
				left join [gmc].[tbl_ELSR] e on e.ELSRID=a.ELSRID
				where 
				year(a.CreateOn)=@RYear 			
				order by a.RCTestingID
			  END

END
GO

IF OBJECT_ID(N'[gmc].[USP_Rpt_WaterBooking_Consolidation]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Rpt_WaterBooking_Consolidation] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Rpt_WaterBooking_Consolidation]
@Flag varchar(50)=null,
@FromDate datetime=null,
@ToDate datetime=null,
@RMonth int=null,
@RYear int=null,
@FDate varchar(10)=null,
@TDate varchar(10)=null,
@valueFlag varchar(50)=null

AS
BEGIN

set nocount on;

Declare @CurrentDate datetime=getdate();

IF @Flag='GetDataByMonthYear'
BEGIN
   
  ;with CTE AS(select a.WaterTankerID,a.TankerStatus,a.BookingDate from [gmc].[tblWaterTankerBookingDtls] a with (nolock) 
                where month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear )
  
  select
  (select count(WaterTankerID) from CTE with (nolock) ) as 'Sec1',
  --Dispose 
  (select count(WaterTankerID) from CTE with (nolock) where TankerStatus=3 ) as 'Dispose_Sec1',
  --Pending  
  (select count(WaterTankerID) from CTE with (nolock) where TankerStatus in(1,2)) as 'Pending_Sec1'



END

IF @Flag='GetDataByYear'
BEGIN
   
  ;with CTE AS(select a.WaterTankerID,a.TankerStatus,a.BookingDate from [gmc].[tblWaterTankerBookingDtls] a with (nolock)  
                where  year(a.BookingDate)=@RYear )  
  select (select count(WaterTankerID) from CTE with (nolock) ) as 'Sec1',  
  --Dispose 
  (select count(WaterTankerID) from CTE with (nolock) where TankerStatus=3 ) as 'Dispose_Sec1',
  --Pending
  (select count(WaterTankerID) from CTE with (nolock) where TankerStatus in(1,2)  ) as 'Pending_Sec1'
  
END

IF @Flag='GetDataByTwoDates'
BEGIN
   
  ;with CTE AS( select a.WaterTankerID,a.TankerStatus,a.BookingDate from [gmc].[tblWaterTankerBookingDtls] a with (nolock) 
               where  CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) )
  
  select
  (select count(WaterTankerID) from CTE with (nolock)) as 'Sec1',  
  --Dispose Befeore Due Dates
  (select count(WaterTankerID) from CTE with (nolock) where TankerStatus=3 ) as 'Dispose_Sec1',  
    --Pending Befeore Due Dates
  (select count(WaterTankerID) from CTE with (nolock)  where TankerStatus in(1,2) ) as 'Pending_Sec1'
  

END

IF @valueFlag='Sec_Tot'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				order by a.WaterTankerID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				order by a.WaterTankerID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
		
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				year(a.BookingDate)=@RYear 
				order by a.WaterTankerID
			  END
END
IF @valueFlag='Sec_D'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.TankerStatus=3
				order by a.WaterTankerID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
				
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.TankerStatus=3
				order by a.WaterTankerID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.TankerStatus=3
				order by a.WaterTankerID
			  END
END
IF @valueFlag='Sec_P'
BEGIN

        set @FromDate =convert(datetime,@FDate,103)
        set @ToDate =convert(datetime,@TDate,103)

        IF @Flag='monthyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				month(a.BookingDate)=@RMonth and year(a.BookingDate)=@RYear 
				and a.TankerStatus in(1,2)
				order by a.WaterTankerID
			  END
        else IF @Flag='daterange'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
				
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				CAST(a.BookingDate AS DATE) between CAST(@FromDate as date) and CAST(@ToDate as DATE) 
				and a.TankerStatus in(1,2)
				order by a.WaterTankerID asc
			  END  
         else IF @Flag='onlyyear'
		BEGIN
				select  a.WaterTankerID , --a.UserID,
				u.FullName as 'Name',cast(u.UserName as varchar) as 'Mobile',
			
				'HNo:'+ad.HNo+', Building_Name:'+ad.Building_Name +' ,StreetOrLineNum:'+ad.StreetOrLineNum+',AreaName:'+ad.Area_Name+',LandMark:'+ad.LandMark+',City:'+ad.City+',nickname:'+ad.nickname as 'Address',
				--c.SubGrievanceName,a.Description,					
				m.FullName as 'ConcernedClerk',
				a.TotalAmount,
				convert(varchar(10),a.BookingDate,103) as 'WaterTankerBookingReceivedDate',
				convert(varchar(10),a.ClosedDate,103) as 'WaterTankerBookingClosedDate'
				from [gmc].[tblWaterTankerBookingDtls] a 
				left join [gmc].[TblAppUser] u on u.ID=a.UserID
				left join [gmc].[tblAddress] ad on a.AddressID=ad.AddressID
			
				left join [gmc].[TblEMPUser] m on  m.EMPID=a.UpdatedBy
				where 
				year(a.BookingDate)=@RYear 
				and a.TankerStatus in(1,2)
				order by a.WaterTankerID
			  END
END

END

  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByMonthYear',@RMonth=3,@RYear=2025
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByYear',@RYear=2025
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation] @Flag='GetDataByTwoDates',@FromDate='2025-05-01 10:13:29.003',@ToDate='2025-05-18 10:13:29.003'
  --exec  [gmc].[USP_Rpt_WaterBooking_Consolidation]  @Flag='onlyyear',@valueFlag='Sec_Tot',@RYear=2024

--select * from [gmc].[tblWaterTankerBookingDtls]

--select UserGrievanceID,CreatedOn from [gmc].[TblUserGrievance] order by CreatedOn
GO

IF OBJECT_ID(N'[gmc].[USP_Save_Beneficiary]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Save_Beneficiary] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Save_Beneficiary]
(
    @Name NVARCHAR(250),
    @Gender NVARCHAR(20) = NULL,
    @DOB DATE = NULL,
    @Age INT = NULL,
    @FatherMotherGuardianName NVARCHAR(250) = NULL,
    @MaritalStatus NVARCHAR(50) = NULL,
    @MobileNumber NVARCHAR(20) = NULL,
    @Aadhaar NVARCHAR(20) = NULL,
    --@ImagePath NVARCHAR(500) = NULL,
    --@ImageFileName NVARCHAR(250) = NULL,
    --@ImageTemplate NVARCHAR(MAX) = NULL,
    --@FRSStatus NVARCHAR(50) = NULL,
    @Home_ID INT=null

   
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;



     declare @SuccessCode varchar(3) 
     declare @SuccessMessage NVARCHAR(500) 

    BEGIN TRY

        BEGIN TRANSACTION;

        /* ==========================================
           Validate Home
           ========================================== */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_Homes]
            WHERE Home_ID = @Home_ID
           
              AND Current_Status = 'Active'
        )
        BEGIN
            SET @SuccessCode = '111';
            SET @SuccessMessage = 'Invalid or inactive Welfare Home.';


            select @SuccessCode as 'Status',@SuccessMessage as 'Message'
            ROLLBACK TRANSACTION;
            RETURN;
        END


        /* ==========================================
           Insert Beneficiary
           ========================================== */

        INSERT INTO [gmc].[tbl_BeneficiaryRegistration]
        (
            Name,
            Gender,
            DOB,
            Age,
            FatherMotherGuardianName,
            MaritalStatus,
            MobileNumber,
            Aadhaar,
            --ImagePath,
            --ImageFileName,
            --ImageTemplate,
            --FRSStatus,
            Home_ID,
            CreatedDate,           
            Is_Active
        )
        VALUES
        (
            @Name,
            @Gender,
            @DOB,
            @Age,
            @FatherMotherGuardianName,
            @MaritalStatus,
            @MobileNumber,
            @Aadhaar,
            --@ImagePath,
            --@ImageFileName,
            --@ImageTemplate,
            --@FRSStatus,
            @Home_ID,
            GETDATE(),            
            1
        );


        DECLARE @BeneficiaryID INT;

        SET @BeneficiaryID = SCOPE_IDENTITY();


        COMMIT TRANSACTION;


        /* ==========================================
           Success
           ========================================== */

        SET @SuccessCode = '000';

        SET @SuccessMessage =
            'Beneficiary registered successfully.';


        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            @BeneficiaryID AS BeneficiaryID;

    END TRY

    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @SuccessCode = '222';

        SET @SuccessMessage = ERROR_MESSAGE();

        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            0 AS BeneficiaryID;


    END CATCH
END;
GO

IF OBJECT_ID(N'[gmc].[USP_Save_BeneficiaryAttendance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Save_BeneficiaryAttendance] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Save_BeneficiaryAttendance]
(
    @BeneficiaryID INT,
    @Home_ID INT,   
    @AttendanceType CHAR(1),   
    @Latitude DECIMAL(10,7) = NULL,
    @Longitude DECIMAL(10,7) = NULL

    
)
AS
BEGIN

    SET NOCOUNT ON;
    SET XACT_ABORT ON;


     declare @SuccessCode varchar(3) 
     declare @SuccessMessage NVARCHAR(500)


    BEGIN TRY

        BEGIN TRANSACTION;


        /* =========================================
           Validate Attendance Type
           ========================================= */

        IF @AttendanceType NOT IN ('M', 'E')
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'Invalid attendance type. Only Morning or Evening is allowed.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                0 AS AttendanceID;

            RETURN;

        END


        /* =========================================
           Check Beneficiary
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryRegistration]
            WHERE BeneficiaryID = @BeneficiaryID
              AND Is_Active = 1
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'Beneficiary not found or inactive.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                0 AS AttendanceID;

            RETURN;

        END


        /* =========================================
           Check Beneficiary belongs to Home
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryRegistration]
            WHERE BeneficiaryID = @BeneficiaryID
              AND Home_ID = @Home_ID
              AND Is_Active = 1
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'Beneficiary does not belong to the selected Welfare Home.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                0 AS AttendanceID;

            RETURN;

        END


        /* =========================================
           Check Duplicate Attendance
           ========================================= */

        IF EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryAttendance]
            WHERE BeneficiaryID = @BeneficiaryID
              AND AttendanceDate = cast(GETDATE() as date)
              AND AttendanceType = @AttendanceType
              AND Is_Active = 1
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                @AttendanceType +
                ' attendance already marked for this beneficiary on this date.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                0 AS AttendanceID;

            RETURN;

        END


       
       


        /* =========================================
           Insert Attendance
           ========================================= */

        INSERT INTO [gmc].[tbl_BeneficiaryAttendance]
        (
            BeneficiaryID,
            Home_ID,
            AttendanceDate,
            AttendanceType,
            AttendanceTime,
            Latitude,
            Longitude,
            CreatedDate,            
            Is_Active
        )
        VALUES
        (
            @BeneficiaryID,
            @Home_ID,
            cast(GETDATE() as date),
            @AttendanceType,
            GETDATE(),
            @Latitude,
            @Longitude,
            GETDATE(),       
            1
        );


        DECLARE @AttendanceID BIGINT;

        SET @AttendanceID = SCOPE_IDENTITY();


        /* =========================================
           Commit
           ========================================= */

        COMMIT TRANSACTION;


        /* =========================================
           Success
           ========================================= */

        SET @SuccessCode = '000';

        SET @SuccessMessage =
            @AttendanceType +
            ' attendance marked successfully.';


        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            @AttendanceID AS AttendanceID;


    END TRY

    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;


        SET @SuccessCode = '222';

        SET @SuccessMessage = ERROR_MESSAGE();


        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            0 AS AttendanceID;

    END CATCH

END;
GO

IF OBJECT_ID(N'[gmc].[USP_Save_Bulk_BeneficiaryAttendance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Save_Bulk_BeneficiaryAttendance] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Save_Bulk_BeneficiaryAttendance]
(
    @BeneficiaryList [gmc].[BeneficiaryAttendanceTVP] READONLY,

    @Home_ID INT, 

    @AttendanceType VARCHAR(1),  

    @Latitude DECIMAL(10,7) = NULL,

    @Longitude DECIMAL(10,7) = NULL

   
)
AS
BEGIN

    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY

        BEGIN TRANSACTION;


        /* =========================================
           Validate Attendance Type
           M = Morning
           E = Evening
           ========================================= */

           declare  @SuccessCode varchar(10)

            declare  @SuccessMessage NVARCHAR(500) 

        IF @AttendanceType NOT IN ('M', 'E')
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'Invalid Attendance Type. Only M or E is allowed.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS SuccessCode,
                @SuccessMessage AS SuccessMessage;

            RETURN;

        END


        /* =========================================
           Validate Attendance Time
           ========================================= */

        --IF @AttendanceTime IS NULL
        --BEGIN
        --    SET @AttendanceTime = GETDATE();
        --END


        /* =========================================
           Check Home
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_Homes]
            WHERE Home_ID = @Home_ID
             -- AND Is_Active = 1
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'Welfare Home not found or inactive.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS SuccessCode,
                @SuccessMessage AS SuccessMessage;

            RETURN;

        END


        /* =========================================
           Check TVP
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM @BeneficiaryList
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'No beneficiaries supplied for attendance.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS SuccessCode,
                @SuccessMessage AS SuccessMessage;

            RETURN;

        END


        /* =========================================
           Validate All Beneficiaries

           Must:
           1. Exist
           2. Be Active
           3. Belong to Home
           ========================================= */

        IF EXISTS
        (
            SELECT 1
            FROM @BeneficiaryList tvp

            LEFT JOIN [gmc].[tbl_BeneficiaryRegistration] b
                ON b.BeneficiaryID = tvp.BeneficiaryID
                AND b.Home_ID = @Home_ID
                AND b.Is_Active = 1

            WHERE b.BeneficiaryID IS NULL
        )
        BEGIN

            SET @SuccessCode = '111';

            SET @SuccessMessage =
                'One or more beneficiaries are invalid, inactive, or do not belong to the selected Welfare Home.';

            ROLLBACK TRANSACTION;

            SELECT
                @SuccessCode AS SuccessCode,
                @SuccessMessage AS SuccessMessage;

            RETURN;

        END


        /* =========================================
           UPDATE EXISTING ATTENDANCE
           ========================================= */

        UPDATE a
        SET
            a.AttendanceTime = getdate(),
            a.Latitude = @Latitude,
            a.Longitude = @Longitude,
            a.ModifiedDate = GETDATE(),
            a.Is_Active = 1

        FROM [gmc].[tbl_BeneficiaryAttendance] a

        INNER JOIN @BeneficiaryList tvp
            ON tvp.BeneficiaryID = a.BeneficiaryID

        WHERE a.Home_ID = @Home_ID
          AND a.AttendanceDate = cast(GETDATE() as date)
          AND a.AttendanceType = @AttendanceType;


        /* =========================================
           INSERT NEW ATTENDANCE
           ========================================= */

        INSERT INTO [gmc].[tbl_BeneficiaryAttendance]
        (
            BeneficiaryID,
            Home_ID,
            AttendanceDate,
            AttendanceType,
            AttendanceTime,
            Latitude,
            Longitude,
            CreatedDate,
            ModifiedDate,
            Is_Active
        )
        SELECT
            tvp.BeneficiaryID,
            @Home_ID,
            cast(GETDATE() as date),
            @AttendanceType,
            GETDATE(),
            @Latitude,
            @Longitude,
            GETDATE(),
            NULL,
            1

        FROM @BeneficiaryList tvp

        WHERE NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryAttendance] a
            WHERE a.BeneficiaryID = tvp.BeneficiaryID
              AND a.Home_ID = @Home_ID
              AND a.AttendanceDate =cast(GETDATE() as date)
              AND a.AttendanceType = @AttendanceType
        );


        /* =========================================
           SUCCESS
           ========================================= */

        COMMIT TRANSACTION;

        SET @SuccessCode = '000';

        SET @SuccessMessage =
            'Bulk beneficiary attendance saved successfully.';


        SELECT
            @SuccessCode AS SuccessCode,
            @SuccessMessage AS SuccessMessage;

    END TRY

    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @SuccessCode = '222';

        SET @SuccessMessage = ERROR_MESSAGE();

        SELECT
            @SuccessCode AS SuccessCode,
            @SuccessMessage AS SuccessMessage;

    END CATCH

END;
GO

IF OBJECT_ID(N'[gmc].[USP_TruckDrver_MasterList]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_TruckDrver_MasterList] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_TruckDrver_MasterList]
@TruckDriverID int=null,
@Flag varchar(100)=null

AS
BEGIN

  IF @Flag = 'CnDAssignedListForTruckDriver'
  BEGIN
    select a.CnDAssignmentID,b.CnDBookingID,b.WasteCategoryID,e.WasteCategoryName,b.WasteTypeID,f.WasteTypeName,
	convert(varchar(10),a.AssignmentDate,103) as 'AssignmentDate',
	b.QtyInTons,b.TotalPrice,
	b.BookingTime,b.BookingDate

	from [gmc].[TblCnDAssignments] a
	join [gmc].[tblCnDBookingDtls] b on a.CnDBookingID=b.CnDBookingID
	--join  [gmc].[TblCnDTruckDriverDtls] c on a.TruckDriverId=c.TruckDriverID
	join [gmc].[tblAddress] d on b.AddressID=d.AddressID
	join [gmc].[TblWasteCategory]  e on e.WasteCategoryID=b.WasteCategoryID
	join [gmc].[TblWasteType] f on f.WasteTypeID=b.WasteTypeID
	where a.TruckDriverID=@TruckDriverID and a.CndStatus='Assigned'
  END

END
GO

IF OBJECT_ID(N'[gmc].[USP_Update_Beneficiary_FRS]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_Update_Beneficiary_FRS] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_Update_Beneficiary_FRS]
(
    @BeneficiaryID INT,
    @ImagePath NVARCHAR(500) = NULL,
    @ImageFileName NVARCHAR(250) = NULL,
    @ImageTemplate NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;


      declare @SuccessCode varchar(3) 
     declare @SuccessMessage NVARCHAR(500) 

    BEGIN TRY

        /* =========================================
           Check Beneficiary
           ========================================= */

        IF NOT EXISTS
        (
            SELECT 1
            FROM [gmc].[tbl_BeneficiaryRegistration]
            WHERE BeneficiaryID = @BeneficiaryID
              AND Is_Active = 1
        )
        BEGIN
            SET @SuccessCode = '111';
            SET @SuccessMessage =
                'Beneficiary not found or inactive.';

            SELECT 
                @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
                @BeneficiaryID AS BeneficiaryID;

            RETURN;
        END


        /* =========================================
           Update FRS Details Only
           ========================================= */

        UPDATE [gmc].[tbl_BeneficiaryRegistration]
        SET
            ImagePath = @ImagePath,
            ImageFileName = @ImageFileName,
            ImageTemplate = @ImageTemplate,
            FRSStatus = 1,
            FRS_UpdatedOn = GETDATE()
        WHERE BeneficiaryID = @BeneficiaryID
          AND Is_Active = 1;


        /* =========================================
           Success
           ========================================= */

        SET @SuccessCode = '000';

        SET @SuccessMessage =
            'Beneficiary FRS details updated successfully.';


        SELECT
            @SuccessCode AS 'Status',
            @SuccessMessage AS 'Message',
            @BeneficiaryID AS BeneficiaryID;

    END TRY

    BEGIN CATCH

        SET @SuccessCode = '222';

        SET @SuccessMessage = ERROR_MESSAGE();

        SELECT
           @SuccessCode AS 'Status',
                @SuccessMessage AS 'Message',
            @BeneficiaryID AS BeneficiaryID;

    END CATCH

END;
GO

IF OBJECT_ID(N'[gmc].[USP_UpdateStatus_TTDFRCCapture]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_UpdateStatus_TTDFRCCapture] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_UpdateStatus_TTDFRCCapture]
@ID int =null
AS
BEGIN

Declare @MobCount int



set @MobCount= (select count(ID) from [gmc].[TblTTDFRSCapture] where ID=@ID) 

print @MobCount


IF (@MobCount > 0 )
BEGIN
     update [gmc].[TblTTDFRSCapture] set Status=1,ModifiedOn=GETDATE() where ID=@ID
	 select '000' as 'Status'
   
END

   select '111' as 'Status'

END
GO

IF OBJECT_ID(N'[gmc].[USP_UserLogin]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USP_UserLogin] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USP_UserLogin]
    @Username NVARCHAR(100) = NULL,
    @Flag VARCHAR(50) = NULL,
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* =====================================================
       GET USER
       ===================================================== */
    IF @Flag = 'GetUser'
    BEGIN

        SELECT
            u.User_ID,
            u.FullName,
            u.Username,
            u.[Password],
            u.UserType_ID,
            ut.UserType_Name,

            u.Map_Home_ID,

            h.Home_Name,
            h.Registration_Number,
            h.Current_Status,

            u.CreatedDate,
            u.Is_Active,
            u.LastLogin,
            u.Failed_Attempts,
            u.Last_Failed_Attempt,
            u.Lockout_Until

        FROM [gmc].[tbl_users] u

        INNER JOIN [gmc].[tbl_UserTypes] ut
            ON ut.UserType_ID = u.UserType_ID

        LEFT JOIN [gmc].[tbl_Homes] h
            ON h.Home_ID = u.Map_Home_ID

        WHERE u.Username = @Username;

    END


    /* =====================================================
       INCREMENT LOGIN FAILURES
       ===================================================== */
    IF @Flag = 'IncrementLoginFailures'
    BEGIN

        DECLARE @FailedAttempts INT;

        SELECT
            @FailedAttempts = ISNULL(Failed_Attempts, 0) + 1
        FROM [gmc].[tbl_users]
        WHERE User_ID = @UserId;


        UPDATE [gmc].[tbl_users]
        SET
            Failed_Attempts = @FailedAttempts,

            Last_Failed_Attempt = GETDATE(),

            Lockout_Until =
                CASE
                    WHEN @FailedAttempts >= 5
                    THEN DATEADD(MINUTE, 30, GETDATE())
                    ELSE NULL
                END

        WHERE User_ID = @UserId;

    END


    /* =====================================================
       RESET LOGIN FAILURES
       ===================================================== */
    IF @Flag = 'ResetLoginFailures'
    BEGIN

        UPDATE [gmc].[tbl_users]
        SET
            Failed_Attempts = 0,
            Last_Failed_Attempt = NULL,
            Lockout_Until = NULL,
            LastLogin = GETDATE()

        WHERE User_ID = @UserId;

    END

END
GO

IF OBJECT_ID(N'[gmc].[USPEMPLogin]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPEMPLogin] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPEMPLogin]

@UserName varchar(10),
@Password nvarchar(500)

As
BEGIN

set nocount on;

IF EXISTS(select 1 from [gmc].[TblEMPUser] with(nolock) where USerName=@UserName and Password=@Password and ISActive=1)
   BEGIN
      select '000' as 'Status',EMPID,FullName,Mobile,Section as 'SectionID',UserType as 'Desig',Role from [gmc].[TblEMPUser] with(nolock) where USerName=@UserName and Password=@Password and ISActive=1
   END
ELSE
BEGIN
     select '111' as 'Status',0 as 'EMPID','' as 'FullName','' as Mobile ,0 as 'SectionID',0 as 'Desig','' as Role
    
END

END


--Note:--  we mentioned Emp Designation as  User Type EMPUsers Table
--select * from [gmc].[TblEMPUser]
GO

IF OBJECT_ID(N'[gmc].[USPGetAddress]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetAddress] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetAddress]
@UserID int=null

As

--EXEC [gmc].[USPGetAddress] @UserID=1
set nocount on

BEGIN
;with Addr as(
select 
b.AddressID,b.UserID,a.FullName,b.HNo,b.Building_Name,b.StreetOrLineNum,b.Area_Name,b.mobile
,b.LandMark,b.City,b.nickname,b.CreatedOn,b.ModifiedBy,b.Langitude
,b.Latitude

,'HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City+',nickname:'+b.nickname as 'Address'
,b.SecretariatID
,s.SecretariatName
,b.WardID
,w.WardName,  RANK() OVER ( ORDER BY  b.CreatedOn Desc) as 'SNO'
 		
from [gmc].[tblAddress] b with(nolock) 
inner join [gmc].[TblAppUser] a with(nolock) on a.ID=b.UserID 
inner join [gmc].[TblSecretariat] s  with(nolock) on s.SecretariatID=b.SecretariatID 
inner join [gmc].[TblWards] w with(nolock) on w.WardID=b.WardID and w.WardID=s.WardID
where a.ID=@UserID )

select SNO	,AddressID	,UserID	,FullName	,HNo	,Building_Name	,StreetOrLineNum	,Area_Name	,
mobile	,LandMark	,City	,nickname	,CreatedOn	,ModifiedBy	,Langitude	,Latitude	,
Address	,SecretariatID	,SecretariatName	,WardID	,WardName

 from Addr

--order by b.AddressID

--AddressID,UserID, HNo,Building_Name,StreetOrLineNum,Area_Name,
--							LandMark,City,nickname,ModifiedBy,Langitude,Latitude from


END


--select * from  [gmc].[tblAddress] where UserID=1 order by CreatedOn Desc
GO

IF OBJECT_ID(N'[gmc].[USPGetBookWaterTanker]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetBookWaterTanker] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetBookWaterTanker]
@UserID int=null

As


set nocount on
--EXEC [gmc].[USPGetBookWaterTanker] 1


BEGIN
select 
a.WaterTankerID,
a.BookingType ,a.OneThousendLtrs,
a.FiveThousendLtrs,a.TenThousendLtrs,a.TotalLtrs,a.TotalAmount,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
a.BookingTime,
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
case when a.TankerStatus=1 then 'Active' when a.TankerStatus=2 then 'Delivered' when a.TankerStatus=3 then 'Cancelled' else 'NA' end as 'TankerStatus',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

a.Langitude,a.Latitude from [gmc].[tblWaterTankerBookingDtls] a
inner join 		
[gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID where a.UserID=@UserID  and a.AddressID=b.AddressID order by a.WaterTankerID

--AddressID,UserID, HNo,Building_Name,StreetOrLineNum,Area_Name,
--							LandMark,City,nickname,ModifiedBy,Langitude,Latitude from


END

--select * from [gmc].[tblWaterTankerBookingDtls] where UserID=1

--select * from [gmc].[tblAddress]
GO

IF OBJECT_ID(N'[gmc].[USPGetMaster]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetMaster] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetMaster]
@EMPID int=null,
@Flag varchar(500)=null
AS
BEGIN

set nocount on;


IF @Flag='GetEmpByEMP'
BEGIN
    select EMPID,FullName,Mobile,case when Section=1 then 'Administration' else '' end as 'Section',case when IsActive=1 then 'Active' else 'Inactive' end as 'EmpStatus' from [gmc].[TblEMPUser] where EMPID=@EMPID
END

IF @Flag='GetGrievance'
BEGIN

			select  'Data found' as 'Status', a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,
			  case when a.GrievanceStatus=1 then 'Active' else 'Closed' end as 'GrievanceStatus',a.Latitude,a.Langitude,cast(day(CreatedOn) as varchar) as 'Day',cast(month(CreatedOn) as varchar) + cast(year(CreatedOn) as varchar) as 'MonthYear'
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
		      order by a.UserID, a.GrievanceStatus asc
END
END
GO

IF OBJECT_ID(N'[gmc].[USPGetOTPs]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetOTPs] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetOTPs]

AS
BEGIN
  select Mobile,OTP,convert (varchar(10),CreatedDate,103) as 'Date',CONVERT(VARCHAR(8),CreatedDate,108)  as 'Time' from [gmc].[tbl_OTP] order by ID Desc
END
GO

IF OBJECT_ID(N'[gmc].[USPGetTTDFRCCapture]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetTTDFRCCapture] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetTTDFRCCapture]
@ID int  = null,
@Flag varchar(100) = null
AS
BEGIN
IF @Flag = 'GetAllImg'
BEGIN
   --select top 200000 ID,Name,Mobile,Image,
   --Template,
   --Status,Aadhar, Place ,convert(varchar(10),SlatDate,103) as SlatDate,SlatTime from [gmc].[TblTTDFRSCapture] order by ID
   Declare @MinRange int
   Declare @MaxRange int

--   IF @ID =  1
--   BEGIN
--      set @MinRange = 1
--  set @MaxRange = 999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  2
--   BEGIN
--      set @MinRange = 1000
--  set @MaxRange = 1999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  3
--   BEGIN
--      set @MinRange = 2000
--  set @MaxRange = 2999
--  print @MinRange 
--  print @MaxRange
--   END   
--   Else IF @ID =  4
--   BEGIN
--      set @MinRange = 3000
--  set @MaxRange = 3999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  5
--   BEGIN
--      set @MinRange = 4000
--  set @MaxRange = 4999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  6
--   BEGIN
--      set @MinRange = 5000
--  set @MaxRange = 5999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  7
--   BEGIN
--      set @MinRange = 6000
--  set @MaxRange = 6999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  8
--   BEGIN
--      set @MinRange = 7000
--  set @MaxRange = 7999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  9
--   BEGIN
--      set @MinRange = 8000
--  set @MaxRange = 8999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  10
--   BEGIN
--      set @MinRange = 9000
--  set @MaxRange = 9999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  11
--   BEGIN
--      set @MinRange = 10000
--  set @MaxRange = 10999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  12
--   BEGIN
--      set @MinRange = 11000
--  set @MaxRange = 11999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  13
--   BEGIN
--      set @MinRange = 12000
--  set @MaxRange = 12999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  13
--   BEGIN
--      set @MinRange = 12000
--  set @MaxRange = 12999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  14
--   BEGIN
--      set @MinRange = 13000
--  set @MaxRange = 13999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  15
--   BEGIN
--      set @MinRange = 14000
--  set @MaxRange = 14999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  16
--   BEGIN
--      set @MinRange = 15000
--  set @MaxRange = 15999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  17
--   BEGIN
--      set @MinRange = 16000
--  set @MaxRange = 16999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  18
--   BEGIN
--      set @MinRange = 17000
--  set @MaxRange = 17999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  19
--   BEGIN
--      set @MinRange = 18000
--  set @MaxRange = 18999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  20
--   BEGIN
--      set @MinRange = 19000
--  set @MaxRange = 19999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  21
--   BEGIN
--      set @MinRange = 20000
--  set @MaxRange = 20999
--  print @MinRange 
--  print @MaxRange
--   END
--    Else IF @ID =  22
--   BEGIN
--      set @MinRange = 21000
--  set @MaxRange = 21999
--  print @MinRange 
--  print @MaxRange
--   END
--    Else IF @ID =  23
--   BEGIN
--      set @MinRange = 22000
--  set @MaxRange = 22999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  24
--   BEGIN
--      set @MinRange = 23000
--  set @MaxRange = 23999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  25
--   BEGIN
--      set @MinRange = 24000
--  set @MaxRange = 24999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  26
--   BEGIN
--      set @MinRange = 25000
--  set @MaxRange = 25999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  27
--   BEGIN
--      set @MinRange = 26000
--  set @MaxRange = 26999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  28
--   BEGIN
--      set @MinRange = 27000
--  set @MaxRange = 27999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  29
--   BEGIN
--      set @MinRange = 28000
--  set @MaxRange = 28999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  30
--   BEGIN
--      set @MinRange = 29000
--  set @MaxRange = 29999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  31
--   BEGIN
--      set @MinRange = 30000
--  set @MaxRange = 30999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  32
--   BEGIN
--      set @MinRange = 31000
--  set @MaxRange = 31999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  32
--   BEGIN
--      set @MinRange = 31000
--  set @MaxRange = 31999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  33
--   BEGIN
--      set @MinRange = 32000
--  set @MaxRange = 32999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  34
--   BEGIN
--      set @MinRange = 33000
--  set @MaxRange = 33999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  35
--   BEGIN
--      set @MinRange = 34000
--  set @MaxRange = 34999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  36
--   BEGIN
--      set @MinRange = 35000
--  set @MaxRange = 35999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  37
--   BEGIN
--      set @MinRange = 36000
--  set @MaxRange = 36999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  38
--   BEGIN
--      set @MinRange = 37000
--  set @MaxRange = 37999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  39
--   BEGIN
--      set @MinRange = 38000
--  set @MaxRange = 38999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  40
--   BEGIN
--      set @MinRange = 39000
--  set @MaxRange = 39999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  41
--   BEGIN
--      set @MinRange = 40000
--  set @MaxRange = 40999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  42
--   BEGIN
--      set @MinRange = 41000
--  set @MaxRange = 41999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  43
--   BEGIN
--      set @MinRange = 42000
--  set @MaxRange = 42999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  44
--   BEGIN
--      set @MinRange = 43000
--  set @MaxRange = 43999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  45
--   BEGIN
--      set @MinRange = 44000
--  set @MaxRange = 44999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  46
--   BEGIN
--      set @MinRange = 45000
--  set @MaxRange = 45999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  47
--   BEGIN
--      set @MinRange = 46000
--  set @MaxRange = 46999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  48
--   BEGIN
--      set @MinRange = 47000
--  set @MaxRange = 47999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  49
--   BEGIN
--      set @MinRange = 48000
--  set @MaxRange = 48999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  50
--   BEGIN
--      set @MinRange = 49000
--  set @MaxRange = 49999
--  print @MinRange 
--  print @MaxRange
--   END
--   Else IF @ID =  50
--BEGIN
--set @MinRange = 49000
--set @MaxRange = 49999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID =  51
--BEGIN
--set @MinRange = 59000
--set @MaxRange = 59999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID =  52
--BEGIN
--set @MinRange = 60000
--set @MaxRange = 69999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID =  53
--BEGIN
--set @MinRange = 70000
--set @MaxRange = 79999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 54
--BEGIN
--set @MinRange=89000
--set @MaxRange=89999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 55
--BEGIN
--set @MinRange=99000
--set @MaxRange=99999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 56
--BEGIN
--set @MinRange=109000
--set @MaxRange=109999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 57
--BEGIN
--set @MinRange=119000
--set @MaxRange=119999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 58
--BEGIN
--set @MinRange=129000
--set @MaxRange=129999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 59
--BEGIN
--set @MinRange=139000
--set @MaxRange=139999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 60
--BEGIN
--set @MinRange=149000
--set @MaxRange=149999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 61
--BEGIN
--set @MinRange=159000
--set @MaxRange=159999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 62
--BEGIN
--set @MinRange=169000
--set @MaxRange=169999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 63
--BEGIN
--set @MinRange=179000
--set @MaxRange=179999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 64
--BEGIN
--set @MinRange=189000
--set @MaxRange=189999
--print @MinRange
--print @MaxRange
--END
--Else IF @ID= 65
--BEGIN
--set @MinRange=199000
--set @MaxRange=199999
--print @MinRange
--print @MaxRange
--END



   select * from [gmc].[VW_GetTTDFRSDtls] 
   --where ID Between @MinRange and @MaxRange 
   order by ID
END  
   
   

END
GO

IF OBJECT_ID(N'[gmc].[USPGetUserCndBookingDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetUserCndBookingDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetUserCndBookingDtls]
@UserID int=null

As


set nocount on
--EXEC [gmc].[USPGetBookWaterTanker] 1


BEGIN
select 
a.CnDBookingID,
a.WasteCategoryID,c.WasteCategoryName as 'WasteCategory',a.WasteTypeID,t.WasteTypeName as 'WasteType',
a.QtyInTons ,a.TotalPrice,
case when a.BookingDate is not null then convert(varchar(10),a.BookingDate,103) else null end as 'BookingDate',
a.BookingTime,
cast(day(a.BookingDate) as varchar) as 'Day',cast(FORMAT(a.BookingDate, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(a.BookingDate) as varchar),2) as 'MonthYear',
case when a.CnDBookingStatus=1 then '1' when a.CnDBookingStatus=2 then '2'when a.CnDBookingStatus=3 then '3'when a.CnDBookingStatus=4 then '4'when a.CnDBookingStatus=5 then '5' else '0' end as 'CnDBookingStatus',
case when a.CnDBookingStatus=1 then 'Trip booked, ward secretary is confirm the amount for trip' when a.CnDBookingStatus=2 then 'Payment is pending please complete payment'  
                               --when a.CnDBookingStatus=3 then 'Payment Completed By' 
                                 else 'NA' end as 'CnDBookingStatusText',
a.AddressID,
'Name:'+b.nickname+', \n Mobile:'+ b.mobile+
', \n HNo:'+b.HNo+', Building_Name:'+b.Building_Name +' ,StreetOrLineNum:'+b.StreetOrLineNum+',AreaName:'+b.Area_Name+',LandMark:'+b.LandMark+',City:'+b.City as 'Address',

a.Langitude,a.Latitude from [gmc].[tblCnDBookingDtls] a
inner join 		
[gmc].[tblAddress] b with(nolock) on a.UserID=b.UserID
inner join [gmc].[TblWasteCategory] c on c.WasteCategoryID=a.WasteCategoryID
inner join [gmc].[TblWasteType] t on t.WasteTypeID=a.WasteTypeID
where a.UserID=@UserID  and a.AddressID=b.AddressID order by a.CreatedOn Desc


END



--select * from [gmc].[tblAddress]
GO

IF OBJECT_ID(N'[gmc].[USPGetUserDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetUserDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetUserDtls]
@Username varchar(10) =null

AS
BEGIN

--EXEC [gmc].[USPGetUserDtls] 9951054361
set nocount on

IF EXISTS(select 1 from  [gmc].[TblAppUser] a with (nolock) where UserName=@Username)
BEGIN
select top 1 'User Available' as'Status',a.ID ,a.FullName,a.Family_Member_count,a.HNo,Building_Name,a.StreetOrLineNum,a.Area_Name,a.LandMark,a.City,a.Pincode,a.UID,a.Mobile,a.WardID,b.WardName,a.SecretariatID,c.SecretariatName,
'http://103.24.200.119/gmcapi/img/banner1.jpg' as 'banner1',
'http://103.24.200.119/gmcapi/img/banner2.jpg' as 'banner2',
'http://103.24.200.119/gmcapi/img/banner3.jpg' as 'banner3'
from [gmc].[TblAppUser] a with (nolock) 
left join [gmc].[TblWards] b with (nolock)  on b.WardID=a.WardID
left join [gmc].[TblSecretariat] c with (nolock)  on c.SecretariatID=a.SecretariatID 
where UserName=@Username
END

ELSE
BEGIN
    select 'User Not Available' as'Status',0 as ID ,'' as FullName,0 as Family_Member_count,'' as HNo,'' as Building_Name,'' as StreetOrLineNum,'' as Area_Name,'' as LandMark,'' as City,0 as Pincode,'' as UID,'' as Mobile,0 as WardID,'' as WardName,0 as SecretariatID,'' as SecretariatName
END

END
GO

IF OBJECT_ID(N'[gmc].[USPGetUserGrievance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPGetUserGrievance] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPGetUserGrievance]
@UserID int=null

AS
BEGIN

set nocount on

IF EXISTS(select 1 from  [gmc].[TblUserGrievance] where  UserID=@UserID)
BEGIN
		--EXEC [gmc].[USPGetUserGrievance] @UserID=1
        --Declare @MainGrievance varchar(100)
		--Declare @SubGrievance varchar(100)

		select  'Data found' as 'Status',a.UserGrievanceID , a.UserID,cast(a.Username as varchar) as 'Username',a.MainGrievanceID,b.MainGrievanceName,
		      a.SubGrievanceID,c.SubGrievanceName,a.Description,a.ImageBase64,a.Image1 ,                                                                              
			  case when a.GrievanceStatus=1 then 'Active' when a.GrievanceStatus=2 then 'Pending' else 'Completed' end as 'GrievanceStatus',a.Latitude,a.Langitude,cast(day(CreatedOn) as varchar) as 'Day',cast(FORMAT(CreatedOn, 'MMM', 'en-US') as varchar)+'-' + right(cast(year(CreatedOn) as varchar),2) as 'MonthYear',
			  a.EMPImageBase64,a.EMPUpdatedDate
		      from [gmc].[TblUserGrievance] a 
		      left join [gmc].[TblMainGrievance] b on a.MainGrievanceID=b.MainGrievanceID
		      left join [gmc].[TblSubGrievance] c on c.SubGrievanceID=a.SubGrievanceID
		      where UserID=@UserID order by a.GrievanceStatus asc

		
END
ELSE
BEGIN

  select 'Data Not found' as 'Status',0 as	UserID,'' as	Username,0 as 	MainGrievanceID,'' as	MainGrievanceName, 0 as 	SubGrievanceID, '' as 	SubGrievanceName,'' as 	'Description'	, '' as 'ImageBase64','' as Image1,'' as 	'GrievanceStatus','' as Latitude,'' as Langitude, '' as 'Day', '' as 'MonthYear'

END



END


--select * from [gmc].[TblUserGrievance] where UserID=29
--EXEC [gmc].[USPGetUserGrievance] @UserID =29
GO

IF OBJECT_ID(N'[gmc].[USPRegOTP]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPRegOTP] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPRegOTP]
@Mobile varchar(20) =null,
@OTP int = null,
@TmpToken varchar(100)=null


AS
BEGIN

--declare @Mobile bigint = 9502982197
--declare @OTP int = 123456
--EXEC [gmc].[USPRegOTP] @Mobile='9502982197',@OTP=12345,@TmpToken=''

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
Declare @OTPcounts int = 0

select @OTPcounts = count(*) from [gmc].[tbl_OTP] where Mobile=@Mobile and cast (getdate() as date) = cast (CreatedDate as date)

IF @OTPcounts < 50
BEGIN
IF EXISTS (select 1 from [gmc].[TblAppUser] where UserName=@Mobile )
BEGIN
	update [gmc].[TblAppUser] set AttemptOn=GETDATE() where UserName=@Mobile 
	END
ELSE	
BEGIN
	insert into [gmc].[TblAppUser](UserName,Mobile,IsActive)values(@Mobile,@Mobile,2)
 END  
    --update [gmc].[tbl_OTP] set OTPStatus=2,ModifiedDate=GETDATE() where Mobile=@Mobile and OTPStatus=1 and cast (getdate() as date) = cast (CreatedDate as date)
	insert into [gmc].[tbl_OTP] (Mobile,OTP,OTPStatus,CreatedDate,TmpToken)values
			(@Mobile,@OTP,1,getdate(),@TmpToken)

	  set @Status='000' 

END
	ELSE
	BEGIN
	  update [gmc].[tbl_OTP] set OTPStatus=2,ModifiedDate=GETDATE() where Mobile=@Mobile and OTPStatus=1 and cast (getdate() as date) = cast (CreatedDate as date)
	  set @Status='111' 
	END

  
  SELECT @ErrorCode = @@ERROR


	IF (@ErrorCode <> 0)
		GOTO error

	COMMIT TRANSACTION

	SELECT @Status AS 'Status',@TmpToken as 'TmpToken'

	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status','' as 'TmpToken'
		ROLLBACK TRANSACTION
	END

END

--select * from [gmc].[TblAppUser]


--select DATEDIFF(SECOND,  '2024-12-04 14:17:08.640',getdate())

--select getdate()
GO

IF OBJECT_ID(N'[gmc].[USPSaveAddress]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveAddress] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveAddress]
@AddressID int=null,
@UserID	int=null,
@HNo	nvarchar(100)=null,
@Building_Name	nvarchar(100)=null,
@StreetOrLineNum	nvarchar(50)=null,
@Area_Name	nvarchar(200)=null,
@LandMark	nvarchar(200)=null,
@City	nvarchar(100)=null,
@nickname nvarchar(100)=null,
@ModifiedBy	int=null,
@Langitude	varchar(50)=null,
@Latitude	varchar(50)=null,
@pincode	varchar(6)=null,
@WardID int=null,
@SecretariatID int=null,
@mobile varchar(10)=null


AS

BEGIN

BEGIN TRANSACTION



DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;





IF EXISTS (select 1 from [gmc].[tblAddress] where AddressID=@AddressID and UserID=@UserID)
BEGIN
       
       update [gmc].[tblAddress] set ModifiedOn=GETDATE() where AddressID=@AddressID  and UserID=@UserID

       set @Status='111'
END
ELSE	
BEGIN
	  insert into [gmc].[tblAddress](UserID,HNo,Building_Name,StreetOrLineNum,Area_Name,
							LandMark,City,nickname,ModifiedBy,Langitude,Latitude,CreatedOn,AddressStatus,pincode,WardID,SecretariatID,mobile)
							values
							(@UserID,@HNo,@Building_Name,@StreetOrLineNum,@Area_Name,@LandMark,@City,@nickname,
							@ModifiedBy,@Langitude,@Latitude,GETDATE(),1,@pincode,@WardID,@SecretariatID,@mobile)
	  set @Status='000'
 
 END  
    
	     


  SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END




END
GO

IF OBJECT_ID(N'[gmc].[USPSaveEventGarbageDtle]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveEventGarbageDtle] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveEventGarbageDtle]
	@UserID int =null,	
	@EventTypeID int= NULL,
	@EventTimeID int =NULL,
	@EventnoofMemberId int= NULL,	
	@BookingDate varchar(10)= NULL,
	@AddressID int =NULL,	
	@Langitude varchar(50)= NULL,
	@Latitude varchar(50)= NULL
		AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

insert into [gmc].[tbl_EventGarbageCollection] (UserID,	 EventTypeID ,	EventTimeID,	EventnoofMemberId ,BookingDate,EGCollectionStatus,AddressID,CreatedOn)values
                                     (@UserID,	@EventTypeID ,	@EventTimeID,	@EventnoofMemberId ,convert(datetime,@BookingDate,103),1,@AddressID,getdate())

set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END


--select * from [gmc].[tbl_EventGarbageCollection]
GO

IF OBJECT_ID(N'[gmc].[USPSaveFRCCapture]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveFRCCapture] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveFRCCapture]
@Aadhar	nvarchar(12)=null,
@Image1  nvarchar(200)=null,
@ImageBase64	varchar(max)=null
AS
BEGIN
IF EXISTS(select 1 from [gmc].[TblFRSCapture] where Aadhar=@Aadhar)
BEGIN
    select '111' as 'Status'
END

insert into [gmc].[TblFRSCapture](Aadhar,ImageBase64,Image1)values(@Aadhar,@ImageBase64,@Image1)
    select '000' as 'Status'

END
GO

IF OBJECT_ID(N'[gmc].[USPSaveTTDFRCCapture]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveTTDFRCCapture] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveTTDFRCCapture]
@ID int =null,
@Name nvarchar(500) NULL,
@Mobile  nvarchar(10) NULL,
@Image varchar(max) NULL,
@Template varchar(max) NULL,
@Aadhar varchar(12) NULL,
@Place varchar(100)NULL,
@SlatDate nvarchar(10) NULL,
@SlatTime varchar(20)NULL
AS
BEGIN

Declare @MobCount int
Declare @AadharCount int


set @MobCount= (select count(ID) from [gmc].[TblTTDFRSCapture1] where Mobile=@Mobile and Status=0) 
set @AadharCount= (select count(ID) from [gmc].[TblTTDFRSCapture1] where Aadhar=@Aadhar  and Status=0) 
print @MobCount
print @AadharCount

IF (@MobCount > 0 or @AadharCount > 0)
BEGIN
    select '111' as 'Status'
END
ELSE
BEGIN
insert into [gmc].[TblTTDFRSCapture1](Name,Image,Template,Mobile,Aadhar,Place,SlatDate,SlatTime)values(@Name,@Image,@Template,@Mobile,@Aadhar,@Place,convert(datetime,@SlatDate,103),@SlatTime)
    select '000' as 'Status'

END

END
GO

IF OBJECT_ID(N'[gmc].[USPSaveUserCnD]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveUserCnD] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveUserCnD]
	@UserID int =null,	
	@WasteCategoryID int null,
	@WasteTypeID int null,
	--@QtyInTons int null,
	--@TotalPrice decimal(18, 2) null,
	@BookingDate varchar(10) NULL,
	@BookingTime nvarchar(10) NULL,
	--@CnDBookingStatus int NULL,
	@AddressID int NULL,
	@BookingFor varchar(50)  null,
	@Langitude varchar(50) NULL,
	@Latitude varchar(50) NULL
		AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

insert into [gmc].[tblCnDBookingDtls] (UserID,	WasteCategoryID,
WasteTypeID,BookingDate,BookingTime,
CnDBookingStatus,AddressID,BookingFor,Langitude,Latitude,CreatedOn
)values(@UserID,	
@WasteCategoryID,
@WasteTypeID,

convert(datetime,@BookingDate,103),
@BookingTime,
1,
@AddressID,
@BookingFor,
@Langitude,
@Latitude,getdate())

set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END
GO

IF OBJECT_ID(N'[gmc].[USPSaveUserGrievance]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveUserGrievance] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveUserGrievance]
@UserID int = null,
@Username varchar(10) = null,
@MainGrievanceID int = null,
@SubGrievanceID int = null,
@AddressID int=null,
@Description nvarchar(500) = null,
@ImageBase64 nvarchar(max) = null,
@Image1  nvarchar(200) = null,
@Langitude varchar(50) = null,
@Latitude  varchar(50) = null,
@OtherGrievanceText  varchar(200) = null,
@ReceivingModeID int   = null,
@CenterID int   = null,
@ReceivingCenter_Details varchar(500) = null,
@Email varchar(50) = null

AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
DECLARE @SectionID int
DECLARE @SecretariatID int

select @SecretariatID=SecretariatID from [gmc].[tblAddress] where AddressID=@AddressID
select @SectionID=SectionId from [gmc].[TblMainGrievance] where MainGrievanceID=@MainGrievanceID
print @SecretariatID
print @SectionID

insert into [gmc].[TblUserGrievance] (UserID,Username ,MainGrievanceID,
SubGrievanceID,Description,ImageBase64,Image1 ,GrievanceStatus,
CreatedOn,Langitude,Latitude,AddressID,OtherGrievanceText,SecretariatID,SectionId,ReceivingModeID,CenterID,ReceivingCenter_Details,Email,CreateDByHD
)values(@UserID,@Username ,@MainGrievanceID,@SubGrievanceID,@Description,
@ImageBase64,@Image1 ,1,getdate(),@Langitude,@Latitude,@AddressID,@OtherGrievanceText,@SecretariatID,@SectionId,@ReceivingModeID,@CenterID,@ReceivingCenter_Details,@Email,1)
set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END


END
GO

IF OBJECT_ID(N'[gmc].[USPSaveWaterTankerBooing]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPSaveWaterTankerBooing] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPSaveWaterTankerBooing]
@WaterTankerID	int =null,
@UserID	int = null,
@BookingType int = null,
@OneThousendLtrs int = null,
@FiveThousendLtrs int = null,
@TenThousendLtrs int = null,
@TotalLtrs int = null,
@TotalAmount DECIMAL(18, 2) = NULL,
@AddressID int = NULL,
@Langitude	varchar(50) = NULL,
@Latitude	varchar(50) = NULL,
@UpdatedBy   int = NULL,
@BookingDate VARCHAR(10)=null,
@BookingTime NVARCHAR(10)=null


AS

BEGIN

BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;

insert into [gmc].[tblWaterTankerBookingDtls] (UserID	,BookingType ,OneThousendLtrs,
FiveThousendLtrs,TenThousendLtrs,TotalLtrs,TotalAmount,TankerStatus,AddressID,
Langitude,Latitude,CreatedOn,BookingDate,BookingTime)
values
(@UserID	,@BookingType ,@OneThousendLtrs,
@FiveThousendLtrs,@TenThousendLtrs,@TotalLtrs,@TotalAmount,1,@AddressID,
@Langitude,@Latitude,getdate(),convert(datetime,@BookingDate,103),@BookingTime)


set @Status='000'



 SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'
	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END






END
GO

IF OBJECT_ID(N'[gmc].[USPTruckDriverLogin]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPTruckDriverLogin] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPTruckDriverLogin]

@UserName varchar(10),
@Password nvarchar(200)

As
BEGIN

set nocount on;

IF EXISTS(select 1 from [gmc].[TblCnDTruckDriverDtls] with(nolock) where USerName=@UserName and Password=@Password and ISActive=1)
   BEGIN
      select '000' as 'Status',TruckDriverID,DriverName as 'FullName',DriverMobile as 'Mobile' from [gmc].[TblCnDTruckDriverDtls] with(nolock) where USerName=@UserName and Password=@Password and ISActive=1
   END
ELSE
BEGIN
     select '111' as 'Status',0 as 'TruckDriverID','' as 'FullName','' as Mobile 
    
END

END
GO

IF OBJECT_ID(N'[gmc].[USPuserReg]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPuserReg] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPuserReg]
@UserName	varchar(10) = null ,
--@Password	Nvarchar(500) null,
@FullName	Nvarchar(200) = null,
@Family_Member_count	int = null,
@HNo		Nvarchar(100) = null,
@Building_Name  Nvarchar(100) = null,
@StreetOrLineNum nvarchar(50) = null,		
@Area_Name		Nvarchar(200) = null,
@LandMark		Nvarchar(200) = null,
@City		Nvarchar(100)  = null,
@Pincode		int  = null,
@Mobile		varchar(10) =null,
@WardID int=null,
@SecretariatID int=null

AS
BEGIN

--declare @Mobile bigint = 9502982197
--declare @OTP int = 123456


BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
DECLARE @UID nvarchar(11)
DECLARE @ID int




IF EXISTS (select 1 from [gmc].[TblAppUser] where UserName=@UserName )
BEGIN
       select  @ID=ID,@UID=('GMC'+Right('00000000'+cast(ID as varchar(8)),8))    from [gmc].[TblAppUser] where UserName=@UserName  
	   print @ID
	   print @UID
	   update [gmc].[TblAppUser] set UserName=@UserName,FullName=@FullName	,Family_Member_count=@Family_Member_count	,
                             HNo=@HNo	,Building_Name=@Building_Name  ,StreetOrLineNum=@StreetOrLineNum ,
                             Area_Name=@Area_Name	,LandMark=@LandMark	,City=@City	,Pincode=@Pincode,IsActive=1,ModifiedOn=GETDATE(),UID=@UID,WardID=@WardID,SecretariatID=@SecretariatID where UserName=@UserName

       set @Status='000'
END
ELSE	
BEGIN
	set @Status='111';
	set    @ID =0 ;set @UID =''  ;set @FullName='' ;set @Family_Member_count=0  ;set @HNo=''  ;set @Building_Name='' ;set @StreetOrLineNum='' ;set @Area_Name='';set @LandMark =''	 ;set @City = '' ;SET @Pincode = 0
 
 END  
    
	     


  SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status',@ID as 'ID',@UID as 'UID',@FullName	as 'FullName',@Family_Member_count	as 'Family_Member_count',@HNo as 'Hno',@Building_Name  as 'Building_Name',@StreetOrLineNum as 'StreetOrLineNum',@Area_Name	as 'Area_Name'	,
				@LandMark AS 'LandMark'	,@City	AS  'City',@Pincode AS 'Pincode'

	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status',0 as 'ID','' as 'UID',''	as 'FullName',0	as 'Family_Member_count','' as 'Hno',''  as 'Building_Name','' as 'StreetOrLineNum',''	as 'Area_Name'	,
				'' AS 'LandMark'	,''	AS  'City',0 AS 'Pincode'
		ROLLBACK TRANSACTION
	END

END

--select * from [gmc].[TblAppUser]


--select DATEDIFF(SECOND,  '2024-12-04 14:17:08.640',getdate())

--select getdate()
GO

IF OBJECT_ID(N'[gmc].[USPVerifyOTP]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [gmc].[USPVerifyOTP] AS RETURN 0;');
GO

ALTER PROCEDURE [gmc].[USPVerifyOTP]
@Mobile varchar(10)= null,
@OTP int = null,
@TmpToken varchar(100)= null


AS

BEGIN



--*****************************************************
--EXEC [gmc].[USPVerifyOTP] @Mobile = 9502982197,@OTP  = 561774,@TmpToken='d7e26c8e-ddb7-4255-9227-9c53c1eb4b30'
--Declare @Mobile bigint= 9502982197
--Declare @OTP int = 93166
--Declare @TmpToken varchar(100)= '5dc7fe21-2972-405a-b265-5607abbd9186'
--*******************************************************

 SET NOCOUNT ON;        
        
 SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED

Declare @GetOTP int
Declare @OTPStatus int
DEclare @OTPID int
Declare @IsActive int
Declare @CreatedDate datetime
Declare @UserName varchar(100)
Declare @UserID int
Declare @GetTmpToken varchar(50)
DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(26) ;



IF EXISTS(select 1 from [gmc].[tbl_OTP] where ID=(select max(id) from [gmc].[tbl_OTP] where OTPStatus=1 and Mobile=@Mobile))
	BEGIN
	
	BEGIN TRANSACTION;
    BEGIN TRY

	select top 1 @GetOTP=o.OTP,@OTPStatus=o.OTPStatus,@OTPID=o.id,@GetTmpToken=o.TmpToken,@UserID=u.ID,@IsActive=u.ISActive,@UserName=u.UserName from [gmc].[tbl_OTP] o inner join 
	[gmc].[TblAppUser] u on o.Mobile=u.UserName where o.id=(select max(id) from [gmc].[tbl_OTP] where OTPStatus=1 and Mobile=@Mobile)
	
	print @OTP print @OTPStatus print @OTPID print @IsActive print @CreatedDate print @UserID print @GetTmpToken 
	
	if (@OTP = @GetOTP and @TmpToken = @GetTmpToken)
	BEGIN
	     update [gmc].[tbl_OTP] set OTPStatus=0,ModifiedDate=getdate() where id=@OTPID
		 update [gmc].[TblAppUser] set AttemptOn=getdate() where id=@UserID
		 set @Status ='Success'
		 select @Status as 'Status',@UserName as 'UserName',case when @IsActive=1 then 'Exists' when @IsActive=2 then 'NotRegistered' else 'Inactive' end  as 'UserStatus' 
	 END
	  ELSE
		BEGIN
          set @Status ='invalidOTP'
		  select @Status as 'Status',@UserName as 'UserName',case when @IsActive=1 then 'Exists' when @IsActive=2 then 'NotRegistered' else 'Inactive' end  as 'UserStatus'
		END

  COMMIT;
        
  END TRY
  BEGIN CATCH
    -- Rollback the transaction in case of an error
    ROLLBACK;
    set @Status = ERROR_MESSAGE();
	--print @Status
	select @Status as 'Status','' as 'UserName',''  as 'UserStatus'
END CATCH;
		
END
ELSE
BEGIN
    set @Status =  'ResendOTP'
	select @Status as 'Status','' as 'UserName',''  as 'UserStatus'
END


END
GO

IF OBJECT_ID(N'[FIN].[USP_IU_Given_Recieve]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [FIN].[USP_IU_Given_Recieve] AS RETURN 0;');
GO

ALTER PROCEDURE [FIN].[USP_IU_Given_Recieve]
@Flag varchar(50)=null,
@GivenID int  =null,
@CustID	int=null,
@GivenAmount money=null,
@NoofWeeks int=null,
--
@RecievedID int=null,
@RecievedAmount money=null,
@DOR varchar(10)=null,
@DOG varchar(10)=null,
@PaymentType int=null

AS
BEGIN

set nocount on;


IF @Flag='saveGivenAmount'
BEGIN
   insert into   FIN.tblAmountGiven(CustID,GivenAmount,NoofWeeks,Status,CreateDate,DOG,PaymentType)values(@CustID,@GivenAmount,@NoofWeeks,1,getdate(),@DOG,@PaymentType)
   select '000' as 'status'
END
IF @Flag='UpdateGivenAmountStatus'
BEGIN
   update FIN.tblAmountGiven set Status=0,ModifiedDate=getdate() where GivenID=@GivenID
   select '000' as 'status'
END
IF @Flag='saveRecievedAmount'
BEGIN
   insert into FIN.tblAmountRecieved(GivenID,CustID,RecievedAmount ,DOR,CreateDate )values (@GivenID,@CustID,@RecievedAmount,@DOR,GETDATE())
   select '000' as 'status'
END
IF @Flag='ListofCustomers'
BEGIN
   select CustID,Name,Mobile,case when IsActive=1 then 'Active' else 'Incative' end as 'Status' from FIN.tblFinCustomers 
END
IF @Flag='ListoPaymentByCustomer'
BEGIN
	  ;with given AS(
    select b.CustID,b.Name,b.Mobile,a.GivenID,convert(int, round(a.GivenAmount ,0)) as 'GivenAmount',
  a.NoofWeeks,case when a.Status=1 then 'Active' else 'Close' end as Status ,a.DOG,case when a.PaymentType=1 then 'Monthly'  when a.PaymentType=2 then 'Weekly' end as 'PaymentType'
  from FIN.tblAmountGiven a inner join FIN.tblFinCustomers b on a.CustID=b.CustID and a.CustID=@CustID
  ),
  Recieved AS(select r.GivenID, sum(convert(int, round(isnull(r.RecievedAmount,0) ,0))) as 'RecievedAmount' 
               from FIN.tblAmountRecieved r  where CustID=@CustID group by r.GivenID)
  
  select a.*,isnull(b.RecievedAmount,0) as 'RecievedAmount' From given a left join Recieved b on a.GivenID=b.GivenID
  
  
  
  order by GivenID asc
END
IF @Flag='ListofAmountRecievedByCustGivenID'
BEGIN
select b.CustID,b.Name,b.Mobile,a.GivenID,convert(int, round(a.GivenAmount ,0)) as 'GivenAmount',a.NoofWeeks,case when a.Status=1 then 'Active' else 'Inactive' end as Status ,r.RecievedID, convert(int, round(isnull(r.RecievedAmount,0) ,0)) as 'RecievedAmount',r.DOR from FIN.tblAmountRecieved r inner join FIN.tblAmountGiven a on r.GivenID=a.GivenID inner join FIN.tblFinCustomers b on a.CustID=b.CustID where r.GivenID=@GivenID order by r.RecievedID

END

IF @Flag='GetListofPaymentsGiven'
BEGIN

	  ;with given AS(
    select b.CustID,b.Name,b.Mobile,a.GivenID,convert(int, round(a.GivenAmount ,0)) as 'GivenAmount',
  a.NoofWeeks,case when a.Status=1 then 'Active' else 'Close' end as Status ,a.DOG,case when a.PaymentType=1 then 'Monthly'  when a.PaymentType=2 then 'Weekly' end as 'PaymentType'
  from FIN.tblAmountGiven a inner join FIN.tblFinCustomers b on a.CustID=b.CustID 
  ),
  Recieved AS(select r.GivenID, sum(convert(int, round(isnull(r.RecievedAmount,0) ,0))) as 'RecievedAmount' 
               from FIN.tblAmountRecieved r  group by r.GivenID)
  
  select a.*,isnull(b.RecievedAmount,0) as 'RecievedAmount' From given a left join Recieved b on a.GivenID=b.GivenID
  
  
  
  order by GivenID asc
END


END




--select * from FIN.tblAmountGiven


--EXEC [FIN].[USP_IU_Given_Recieve] @Flag='GetListofPaymentsGiven'
GO

IF OBJECT_ID(N'[FIN].[USP_Reg_FIN_Customer]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [FIN].[USP_Reg_FIN_Customer] AS RETURN 0;');
GO

ALTER PROCEDURE [FIN].[USP_Reg_FIN_Customer]
@Name	nvarchar(100)=null,	
@Mobile	varchar(10)=null



AS

set  nocount on;


BEGIN
IF EXISTS(select 1 from FIN.tblFinCustomers where Mobile=@Mobile)
BEGIN
    select '111' as 'status'
END
ELSE
BEGIN
    insert into FIN.tblFinCustomers(Name,Mobile,CreateDate ,IsActive)values(@Name,@Mobile,GETDATE(),1)	
	select '000' as 'status'
END




END
GO

IF OBJECT_ID(N'[SERP].[usp_DashboardReports]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[usp_DashboardReports] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[usp_DashboardReports]
    @Flag NVARCHAR(50),   
	@Date Date = NULL,-- which report to run (string flag)
                  -- used for date-based flags
    @Month INT = NULL,               -- used for monthly report
    @Year INT = NULL,                -- used for monthly report
    @GraceMinutes INT = 15  ,         -- grace for on-time/late
	@EMP_ID int = null,
	@EmpTypeID INT =Null
	--@ClientId INT = NULL
	--------------------
	--exec SERP.usp_DashboardReports @Flag='getDashboardSummary'

AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DefaultInTime TIME = '09:00:00';
    DECLARE @DefaultOutTime TIME = '17:00:00';

	DECLARE @StartDate DATE = DATEFROMPARTS(@Year,@Month,1);
    DECLARE @EndDate DATE = EOMONTH(@StartDate);
    DECLARE @TotalDaysInMonth INT = DAY(@EndDate);

	 --Declare @Date DATE = convert(date,@SelectedDate,103)


    -- default @Date to today if needed
    IF @Date IS NULL AND @Flag IN (
        'getDashboardSummary','getTotalAttended','getTotalLogin','getTotalExit',
        'getEarlyLoginCount','getLateLoginCount','getOnTimeLoginCount',
        'getAttendanceList','getEarlyLoginList','getLateLoginList','getNotAttendanceList'
    )
        SET @Date = CONVERT(date, GETDATE());

    ---------------------------------------------------------
    -- getDashboardSummary
    ---------------------------------------------------------
    IF @Flag = 'getDashboardSummary'
    BEGIN
        ;WITH ShiftResolved AS
        (
            SELECT
                S.ShiftID,
                S.ShiftTime,
                ISNULL(S.InTime, @DefaultInTime) AS InTime,
                ISNULL(S.Outtime, @DefaultOutTime) AS OutTime
            FROM SERP.tbl_Shifts S

            UNION ALL

            SELECT
                0 AS ShiftID,
                '09:00 to 17:00' AS ShiftTime,
                @DefaultInTime AS InTime,
                @DefaultOutTime AS OutTime
        ),
        Att AS
        (
            SELECT
                A.AttendanceID,
                A.EMP_ID,
                A.EntryTime,
                A.ExitTime,
                CONVERT(date, A.EntryTime) AS EntryDate,
                CONVERT(date, A.ExitTime) AS ExitDate,
                E.EMP_Name,
                E.EmpTypeID,
                E.Desig,
                ISNULL(E.ShiftID,0) AS ShiftID,
                S.InTime,
                S.OutTime
            FROM SERP.tbl_EMPDailyAttendance A
            INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID 
            LEFT JOIN ShiftResolved S ON ISNULL(E.ShiftID,0) = S.ShiftID where cast(A.DOA as date)=@Date AND E.IsActive = 1
        ),
		emp AS(
		        SELECT e.EMP_ID,e.EMP_Name FROM SERP.tbl_Employee e left join Att a on a.EMP_ID=e.EMP_ID WHERE e.IsActive = 1
		)
		,Abscent AS(
		        SELECT e.EMP_ID,e.EMP_Name,a.AttendanceID FROM emp e left join Att a on a.EMP_ID=e.EMP_ID 
		)
        ,AWS AS
        (
            SELECT
                a.*,
                CASE
                    WHEN a.InTime <= a.OutTime
                        THEN DATEADD(second, DATEDIFF(second,0,a.InTime), CAST(a.EntryDate AS datetime))
                    ELSE
                        CASE WHEN CAST(a.EntryTime AS time) < a.InTime
                            THEN DATEADD(second, DATEDIFF(second,0,a.InTime), DATEADD(day,-1,CAST(a.EntryDate AS datetime)))
                            ELSE DATEADD(second, DATEDIFF(second,0,a.InTime), CAST(a.EntryDate AS datetime))
                        END
                END AS ShiftStartDateTime
            FROM Att a
            WHERE a.EntryTime IS NOT NULL AND a.EntryDate = @Date
        )

		--select * from Abscent
		,Final AS(
        SELECT
            (SELECT COUNT(*) FROM SERP.tbl_Employee WHERE IsActive = 1) AS TotalActiveEmployees,
			(SELECT COUNT(*) FROM SERP.tbl_Employee WHERE IsActive <> 1) AS TotalInActive,
            (SELECT COUNT(*) FROM SERP.tbl_Employee ) AS TotalEmployees,
            (SELECT COUNT(DISTINCT EMP_ID) FROM Att WHERE EntryDate = @Date AND EntryTime IS NOT NULL ) AS TotalAttended,
            (SELECT COUNT(DISTINCT EMP_ID) FROM Att WHERE EntryDate = @Date AND EntryTime IS NOT NULL) AS TotalLogin,
            (SELECT COUNT(DISTINCT EMP_ID) FROM Att WHERE ExitDate = @Date AND ExitTime IS NOT NULL) AS TotalExit,
            (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE DATEDIFF(MINUTE,ShiftStartDateTime,EntryTime) < 0) AS EarlyLoginCount,
            (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE DATEDIFF(MINUTE,ShiftStartDateTime,EntryTime) > @GraceMinutes) AS LateLoginCount,
            (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE DATEDIFF(MINUTE,ShiftStartDateTime,EntryTime) BETWEEN 0 AND @GraceMinutes) AS OnTimeLoginCount,
			(SELECT COUNT(DISTINCT EMP_ID) FROM  Abscent WHERE AttendanceID IS NULL) AS NotMarked
        )

		select TotalActiveEmployees,	TotalInActive,	TotalEmployees,	TotalAttended,	TotalLogin,	TotalExit,	EarlyLoginCount,	LateLoginCount,	OnTimeLoginCount , NotMarked,
		    CAST((isnull(TotalAttended,0) * 100.0) / NULLIF(TotalActiveEmployees,0) AS DECIMAL(5,2)) AS PresentPercent,
			CAST((isnull(EarlyLoginCount,0) * 100.0) / NULLIF(TotalAttended,0) AS DECIMAL(5,2)) AS EarlyPercent,
			CAST((isnull(LateLoginCount,0) * 100.0) / NULLIF(TotalAttended,0) AS DECIMAL(5,2)) AS LatePercent,
			CAST((isnull(OnTimeLoginCount,0) * 100.0) / NULLIF(TotalAttended,0) AS DECIMAL(5,2)) AS OnTimePercent,
			CAST((isnull(NotMarked,0) * 100.0) / NULLIF(TotalActiveEmployees,0) AS DECIMAL(5,2)) AS NotMarkedPercent
		from final


		RETURN;
    END

    ---------------------------------------------------------
    -- getTotalEmployeesByType
    ---------------------------------------------------------
    IF @Flag = 'getTotalEmployeesByType'
    BEGIN
        SELECT
            ET.EmpTypeID,
            ET.EmpType,
            COUNT(E.EMP_ID) AS EmployeeCount
        FROM SERP.tbl_EMP_Types ET
        LEFT JOIN SERP.tbl_Employee E ON ET.EmpTypeID = E.EmpTypeID where E.IsActive=1
        GROUP BY ET.EmpTypeID, ET.EmpType
        ORDER BY ET.EmpType;
        RETURN;
    END

    ---------------------------------------------------------
    -- getTotalAttended (distinct EMP_ID with EntryTime on @Date)
    ---------------------------------------------------------
    IF @Flag = 'getTotalAttended'
    BEGIN
        SELECT COUNT(DISTINCT A.EMP_ID) AS TotalAttended
        FROM SERP.tbl_EMPDailyAttendance A
        WHERE CONVERT(date, A.EntryTime) = @Date
          AND A.EntryTime IS NOT NULL;
        RETURN;
    END

    ---------------------------------------------------------
    -- getTotalLogin (distinct EntryTime on @Date)
    ---------------------------------------------------------
    IF @Flag = 'getTotalLogin'
    BEGIN
        SELECT COUNT(DISTINCT A.EMP_ID) AS TotalLogin
        FROM SERP.tbl_EMPDailyAttendance A
        WHERE CONVERT(date, A.EntryTime) = @Date
          AND A.EntryTime IS NOT NULL;
        RETURN;
    END

    ---------------------------------------------------------
    -- getTotalExit (distinct ExitTime on @Date)
    ---------------------------------------------------------
    IF @Flag = 'getTotalExit'
    BEGIN
        SELECT COUNT(DISTINCT A.EMP_ID) AS TotalExit
        FROM SERP.tbl_EMPDailyAttendance A
        WHERE CONVERT(date, A.ExitTime) = @Date
          AND A.ExitTime IS NOT NULL;
        RETURN;
    END

    ---------------------------------------------------------
    -- getEarlyLoginCount / getLateLoginCount / getOnTimeLoginCount
    ---------------------------------------------------------
    IF @Flag IN ('getEarlyLoginCount','getLateLoginCount','getOnTimeLoginCount')
    BEGIN
        ;WITH ShiftResolved AS
        (
            SELECT
                S.ShiftID,
                S.ShiftTime,
                ISNULL(S.InTime, @DefaultInTime) AS InTime,
                ISNULL(S.Outtime, @DefaultOutTime) AS OutTime
            FROM SERP.tbl_Shifts S

            UNION ALL

            SELECT
                0 AS ShiftID,
                '09:00 to 17:00' AS ShiftTime,
                @DefaultInTime AS InTime,
                @DefaultOutTime AS OutTime
        ),
        AWS AS
        (
            SELECT
                A.EMP_ID,
                A.EntryTime,
                A.ExitTime,
                CONVERT(date, A.EntryTime) AS EntryDate,
                ISNULL(E.ShiftID,0) AS ShiftID,
                S.InTime,
                S.OutTime,
                CASE
                    WHEN ISNULL(S.InTime, @DefaultInTime) <= ISNULL(S.OutTime, @DefaultOutTime)
                        THEN DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), CAST(CONVERT(date,A.EntryTime) AS datetime))
                    ELSE
                        CASE WHEN CAST(A.EntryTime AS time) < ISNULL(S.InTime,@DefaultInTime)
                            THEN DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), DATEADD(day,-1,CAST(CONVERT(date,A.EntryTime) AS datetime)))
                            ELSE DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), CAST(CONVERT(date,A.EntryTime) AS datetime))
                        END
                END AS ShiftStartDateTime,
                DATEDIFF(MINUTE,
                    CASE
                        WHEN ISNULL(S.InTime,@DefaultInTime) <= ISNULL(S.OutTime,@DefaultOutTime)
                            THEN DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), CAST(CONVERT(date,A.EntryTime) AS datetime))
                        ELSE
                            CASE WHEN CAST(A.EntryTime AS time) < ISNULL(S.InTime,@DefaultInTime)
                                THEN DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), DATEADD(day,-1,CAST(CONVERT(date,A.EntryTime) AS datetime)))
                                ELSE DATEADD(second, DATEDIFF(second,0,ISNULL(S.InTime,@DefaultInTime)), CAST(CONVERT(date,A.EntryTime) AS datetime))
                            END
                    END,
                    A.EntryTime) AS MinutesFromShiftStart
            FROM SERP.tbl_EMPDailyAttendance A
            INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN ShiftResolved S ON ISNULL(E.ShiftID,0) = S.ShiftID
            WHERE CONVERT(date, A.EntryTime) = @Date
              AND A.EntryTime IS NOT NULL AND E.IsActive=1
        )
        SELECT
            @Flag AS Metric,
            CASE
                WHEN @Flag = 'getEarlyLoginCount' THEN (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE MinutesFromShiftStart < 0)
                WHEN @Flag = 'getLateLoginCount'  THEN (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE MinutesFromShiftStart > @GraceMinutes)
                WHEN @Flag = 'getOnTimeLoginCount' THEN (SELECT COUNT(DISTINCT EMP_ID) FROM AWS WHERE MinutesFromShiftStart BETWEEN 0 AND @GraceMinutes)
            END AS Value;
        RETURN;
    END

    ---------------------------------------------------------
    -- getAttendanceList
    ---------------------------------------------------------
    IF @Flag = 'getAttendanceList'
    BEGIN
        SELECT DISTINCT
            E.EMP_ID,
            E.EMP_Name,
            ET.EmpType,
            D.Designation,
            A.EntryTime,
            A.ExitTime,
            ISNULL(S.ShiftTime, '09:00 to 17:00') AS ShiftTime,
            ISNULL(S.InTime, @DefaultInTime) AS InTime,
            ISNULL(S.OutTime, @DefaultOutTime) AS OutTime
        FROM SERP.tbl_EMPDailyAttendance A
        INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
        LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
        LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
        LEFT JOIN (
            SELECT S.ShiftID, S.ShiftTime, S.InTime, S.OutTime FROM SERP.tbl_Shifts S
            UNION ALL SELECT 0, '09:00 to 17:00', @DefaultInTime, @DefaultOutTime
        ) S ON ISNULL(E.ShiftID,0) = S.ShiftID
        WHERE CONVERT(date,A.EntryTime) = @Date AND E.IsActive=1
        ORDER BY A.EntryTime, E.EMP_ID;
        RETURN;
    END

    ---------------------------------------------------------
    -- getEarlyLoginList
    ---------------------------------------------------------
    IF @Flag = 'getEarlyLoginList'
    BEGIN
        ;WITH ShiftResolved AS
        (
            SELECT S.ShiftID, S.ShiftTime, ISNULL(S.InTime,@DefaultInTime) AS InTime, ISNULL(S.Outtime,@DefaultOutTime) AS OutTime
            FROM SERP.tbl_Shifts S
            UNION ALL SELECT 0, '09:00 to 17:00', @DefaultInTime, @DefaultOutTime
        ),
        AWS AS
        (
            SELECT
                A.EMP_ID,
                E.EMP_Name,
                ET.EmpType,
                D.Designation,
                A.EntryTime,
                S.ShiftTime,
				A.ExitTime,
                S.InTime,
                S.OutTime,
                DATEDIFF(MINUTE,
                    CASE
                        WHEN S.InTime <= S.OutTime
                            THEN DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                        ELSE
                            CASE WHEN CAST(A.EntryTime AS time) < S.InTime
                                THEN DATEADD(second, DATEDIFF(second,0,S.InTime), DATEADD(day,-1,CAST(CONVERT(date,A.EntryTime) AS datetime)))
                                ELSE DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                            END
                    END,
                    A.EntryTime) AS MinutesFromShiftStart
            FROM SERP.tbl_EMPDailyAttendance A
            INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
            LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
            LEFT JOIN ShiftResolved S ON ISNULL(E.ShiftID,0) = S.ShiftID
            WHERE CONVERT(date,A.EntryTime) = @Date AND A.EntryTime IS NOT NULL AND E.IsActive=1
        )
        SELECT *
        FROM AWS
        WHERE MinutesFromShiftStart < 0
        ORDER BY MinutesFromShiftStart, EntryTime;
        RETURN;
    END
	    ---------------------------------------------------------
    -- getLateLoginList
    ---------------------------------------------------------
    IF @Flag = 'getLateLoginList'
    BEGIN
        ;WITH ShiftResolved AS
        (
            SELECT S.ShiftID, S.ShiftTime, ISNULL(S.InTime,@DefaultInTime) AS InTime, ISNULL(S.Outtime,@DefaultOutTime) AS OutTime
            FROM SERP.tbl_Shifts S
            UNION ALL SELECT 0, '09:00 to 17:00', @DefaultInTime, @DefaultOutTime
        ),
        AWS AS
        (
            SELECT
                A.EMP_ID,
                E.EMP_Name,
                ET.EmpType,
                D.Designation,
                A.EntryTime,
				A.ExitTime,
                S.ShiftTime,
                S.InTime,
                S.OutTime,
                DATEDIFF(MINUTE,
                    CASE
                        WHEN S.InTime <= S.OutTime
                            THEN DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                        ELSE
                            CASE WHEN CAST(A.EntryTime AS time) < S.InTime
                                THEN DATEADD(second, DATEDIFF(second,0,S.InTime), DATEADD(day,-1,CAST(CONVERT(date,A.EntryTime) AS datetime)))
                                ELSE DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                            END
                    END,
                    A.EntryTime) AS MinutesFromShiftStart
            FROM SERP.tbl_EMPDailyAttendance A
            INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
            LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
            LEFT JOIN ShiftResolved S ON ISNULL(E.ShiftID,0) = S.ShiftID
            WHERE CONVERT(date,A.EntryTime) = @Date AND A.EntryTime IS NOT NULL AND E.IsActive=1
        )
        SELECT
            EMP_ID,
            EMP_Name,
            EmpType,
            Designation,
            EntryTime,
            ShiftTime,
			ExitTime,
            InTime,
            OutTime,
            MinutesFromShiftStart
        FROM AWS
        WHERE MinutesFromShiftStart > @GraceMinutes
        ORDER BY MinutesFromShiftStart, EntryTime;
        RETURN;
    END

    ---------------------------------------------------------
    -- getOnTimeLoginList
    ---------------------------------------------------------
    IF @Flag = 'getOnTimeLoginList'
    BEGIN
        ;WITH ShiftResolved AS
        (
            SELECT S.ShiftID, S.ShiftTime, ISNULL(S.InTime,@DefaultInTime) AS InTime, ISNULL(S.Outtime,@DefaultOutTime) AS OutTime
            FROM SERP.tbl_Shifts S
            UNION ALL SELECT 0, '09:00 to 17:00', @DefaultInTime, @DefaultOutTime
        ),
        AWS AS
        (
            SELECT
                A.EMP_ID,
                E.EMP_Name,
                ET.EmpType,
                D.Designation,
                A.EntryTime,
                S.ShiftTime,
                S.InTime,
                S.OutTime,
				A.ExitTime,
                DATEDIFF(MINUTE,
                    CASE
                        WHEN S.InTime <= S.OutTime
                            THEN DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                        ELSE
                            CASE WHEN CAST(A.EntryTime AS time) < S.InTime
                                THEN DATEADD(second, DATEDIFF(second,0,S.InTime), DATEADD(day,-1,CAST(CONVERT(date,A.EntryTime) AS datetime)))
                                ELSE DATEADD(second, DATEDIFF(second,0,S.InTime), CAST(CONVERT(date,A.EntryTime) AS datetime))
                            END
                    END,
                    A.EntryTime) AS MinutesFromShiftStart
            FROM SERP.tbl_EMPDailyAttendance A
            INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
            LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
            LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
            LEFT JOIN ShiftResolved S ON ISNULL(E.ShiftID,0) = S.ShiftID
            WHERE CONVERT(date,A.EntryTime) = @Date AND A.EntryTime IS NOT NULL AND E.IsActive=1
        )
        SELECT
            EMP_ID,
            EMP_Name,
            EmpType,
            Designation,
            EntryTime,
            ShiftTime,
			ExitTime,
            InTime,
            OutTime,
            MinutesFromShiftStart
        FROM AWS
        WHERE MinutesFromShiftStart BETWEEN 0 AND @GraceMinutes
        ORDER BY MinutesFromShiftStart, EntryTime;
        RETURN;
    END
	--------------------------------------------------------
	--get NotMarkedList
	--------------------------------------------------------
	 IF @Flag = 'getNotAttendanceList'
    BEGIN
	    
		 SELECT DISTINCT
            E.EMP_ID,
            E.EMP_Name,
            ET.EmpType,
            D.Designation
            
        FROM
        SERP.tbl_Employee E 
        LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
        LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
         WHERE E.IsActive = 1
        AND E.EMP_ID not in(
       

        SELECT DISTINCT
            E.EMP_ID          
        FROM SERP.tbl_EMPDailyAttendance A
        INNER JOIN SERP.tbl_Employee E ON A.EMP_ID = E.EMP_ID
        LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
        LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
        LEFT JOIN (
            SELECT S.ShiftID, S.ShiftTime, S.InTime, S.OutTime FROM SERP.tbl_Shifts S
            UNION ALL SELECT 0, '09:00 to 17:00', @DefaultInTime, @DefaultOutTime
        ) S ON ISNULL(E.ShiftID,0) = S.ShiftID
        WHERE CONVERT(date,A.EntryTime) = @Date AND E.IsActive=1
        );
		RETURN;
    END
	


--EXEC SERP.usp_DashboardReports @Flag='getNotAttendanceList', @Date = '2025-11-17';
-----------------------------------------------------------------------------------

 IF (@Flag = 'getTotalEmployeesList')
    BEGIN
        SELECT 
            E.EMP_ID,
            E.EMP_Name,
            T.EmpType,
            E.Desig AS Designation
        FROM SERP.tbl_Employee E
        LEFT JOIN SERP.tbl_EMP_Types T ON E.EmpTypeID = T.EmpTypeID where E.IsActive=1
        ORDER BY E.EMP_ID ASC;
        RETURN;
    END

	--EXEC SERP.usp_DashboardReports @Flag='getTotalEmployeesList';

	IF (@Flag = 'getActiveEmployeesList')
    BEGIN
        SELECT 
            E.EMP_ID,
            E.EMP_Name,
            T.EmpType,
            E.Desig AS Designation
        FROM SERP.tbl_Employee E
        LEFT JOIN SERP.tbl_EMP_Types T ON E.EmpTypeID = T.EmpTypeID
        WHERE E.IsActive = 1
        ORDER BY E.EMP_ID ASC;
        RETURN;
    END
	--EXEC SERP.usp_DashboardReports @Flag='getActiveEmployeesList';

	 IF (@Flag = 'getInactiveEmployeesList')
    BEGIN
        SELECT 
            E.EMP_ID,
            E.EMP_Name,
            T.EmpType,
            E.Desig AS Designation
        FROM SERP.tbl_Employee E
        LEFT JOIN SERP.tbl_EMP_Types T ON E.EmpTypeID = T.EmpTypeID
        WHERE E.IsActive = 0
        ORDER BY E.EMP_ID ASC;
        RETURN;
    END

	--EXEC SERP.usp_DashboardReports @Flag='getInactiveEmployeesList';

    ---------------------------------------------------------
    -- getMonthlyReport
    ---------------------------------------------------------
	IF @Flag = 'getMonthlyReport'
BEGIN
    IF @Month IS NULL OR @Year IS NULL
    BEGIN
        RAISERROR('For getMonthlyReport you must supply @Month and @Year',16,1);
        RETURN;
    END

    SELECT
        E.EMP_ID,
        E.EMP_Name,
        ET.EmpTypeID,
        ET.EmpType AS Designation,
        @TotalDaysInMonth AS TotalDaysInMonth,
        ISNULL((
            SELECT COUNT(DISTINCT CONVERT(date,A2.EntryTime))
            FROM SERP.tbl_EMPDailyAttendance A2
            WHERE A2.EMP_ID = E.EMP_ID
              AND CONVERT(date,A2.EntryTime) BETWEEN @StartDate AND @EndDate
              AND A2.EntryTime IS NOT NULL
        ),0) AS TotalAttendDays
    FROM SERP.tbl_Employee E
    LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
   WHERE E.IsActive = 1
      AND (@EmpTypeID IS NULL OR ET.EmpTypeID = @EmpTypeID)   -- ⭐ Added Filter Here
    ORDER BY E.EMP_ID;

    RETURN;
END

--EXEC SERP.usp_DashboardReports @Flag=getMonthlyReport,@Month=10,@Year=2025,@EmpTypeId=2

    --IF @Flag = 'getMonthlyReport'
    --BEGIN
    --    IF @Month IS NULL OR @Year IS NULL
    --    BEGIN
    --        RAISERROR('For getMonthlyReport you must supply @Month and @Year',16,1);
    --        RETURN;
    --    END

        

    --    SELECT
    --        E.EMP_ID,
    --        E.EMP_Name,
    --        D.EmpTypeID,
    --        @TotalDaysInMonth AS TotalDaysInMonth,
    --        ISNULL((
    --            SELECT COUNT(DISTINCT CONVERT(date,A2.EntryTime))
    --            FROM SERP.tbl_EMPDailyAttendance A2
    --            WHERE A2.EMP_ID = E.EMP_ID
    --              AND CONVERT(date,A2.EntryTime) BETWEEN @StartDate AND @EndDate
    --              AND A2.EntryTime IS NOT NULL
    --        ),0) AS TotalAttendDays
    --    FROM SERP.tbl_Employee E
    --    LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID
    --    ORDER BY E.EMP_ID;
    --    RETURN;
    --END

	--get Month report of Emp

	IF @Flag = 'getMonthlyReportByEMPID'
    BEGIN
        IF @Month IS NULL OR @Year IS NULL
        BEGIN
            RAISERROR('For getMonthlyReport you must supply @Month and @Year',16,1);
            RETURN;
        END

        --DECLARE @StartDate DATE = DATEFROMPARTS(@Year,@Month,1);
        --DECLARE @EndDate DATE = EOMONTH(@StartDate);
        --DECLARE @TotalDaysInMonth INT = DAY(@EndDate);

        SELECT
            E.EMP_ID,
            E.EMP_Name,
            D.Designation,
            @TotalDaysInMonth AS TotalDaysInMonth,
            ISNULL((
                SELECT COUNT(DISTINCT CONVERT(date,A2.EntryTime))
                FROM SERP.tbl_EMPDailyAttendance A2
                WHERE A2.EMP_ID = E.EMP_ID
                  AND CONVERT(date,A2.EntryTime) BETWEEN @StartDate AND @EndDate
                  AND A2.EntryTime IS NOT NULL
            ),0) AS TotalAttendDays
        FROM SERP.tbl_Employee E
        LEFT JOIN SERP.tbl_Designation D ON E.Desig = D.DesigID where e.EMP_ID=@EMP_ID AND E.IsActive=1
        ORDER BY E.EMP_ID;
        RETURN;
    END

	IF @Flag = 'GetEmpTypes'
	BEGIN
		SELECT EmpTypeID, EmpType
		FROM SERP.tbl_EMP_Types
		ORDER BY EmpType;

		RETURN;
	END
--Month report by empid 
 IF @Flag='GetMonthRptByEMPID'
  BEGIN
     SELECT 
    E.EMP_ID,
    E.EMP_Name,
    convert(varchar(10), CONVERT(DATE, A.DOA),103) AS AttendanceDate,
    CONVERT(VARCHAR(8), A.EntryTime, 108) AS InTime,   -- 108 = hh:mm:ss
    CONVERT(VARCHAR(8), A.ExitTime, 108) AS OutTime,
    S.ShiftTime,
    S.InTime AS ShiftIn,
    S.OutTime AS ShiftOut
FROM [SERP].[tbl_EMPDailyAttendance] A
INNER JOIN [SERP].[tbl_Employee] E ON A.EMP_ID = E.EMP_ID
LEFT JOIN [SERP].[tbl_Shifts] S ON ISNULL(E.ShiftID, 0) = S.ShiftID
WHERE A.EMP_ID = @EMP_ID and MONTH(A.DOA)=@Month and Year(A.DOA)=@Year
ORDER BY DOA ASC;
  END

    -- Unknown flag
    RAISERROR('Unknown @Flag value. Please use a valid flag.',16,1);
END


--EXEC SERP.usp_DashboardReports @Flag = 'getMonthlyReport',@Month=9,@Year=2025
GO

IF OBJECT_ID(N'[SERP].[USP_EMP_Registration]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_EMP_Registration] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_EMP_Registration]
@EMP_Name varchar(100)=null,
@Aadhar varchar(12)=null,
@Mobile varchar(10)=null,
@EmpTypeID int=null, 
@Desig int=null, 
@ShiftID int=null,
@ImageBase64 nvarchar(max)=null,
@Template varchar(max)=null,
@ImagePath varchar(200)=null,
@Gender varchar(10)=null,
@StallNum int=null,
@AgencyID int=null

AS
BEGIN

--declare @Mobile bigint = 9502982197
--declare @OTP int = 123456


BEGIN TRANSACTION


DECLARE @ErrorCode VARCHAR(5) = '';
DECLARE @Status VARCHAR(5) ;
DECLARE @UID nvarchar(11)
DECLARE @ID int




IF EXISTS (select 1 from SERP.tbl_Employee where Aadhar=@Aadhar )
BEGIN       
       set @Status='111'
END
ELSE	
BEGIN
   insert into SERP.tbl_Employee(EMP_Name,Aadhar ,Mobile,EmpTypeID , Desig , ShiftID ,ImageBase64 ,Template,IsActive,CreateOn,ImagePath,Gender,StallNum,AgencyID)values
	(@EMP_Name,@Aadhar ,@Mobile,@EmpTypeID , @Desig , @ShiftID ,@ImageBase64 ,@Template,1,GETDATE(),@ImagePath,@Gender,@StallNum,@AgencyID)
	set @Status='000';
 
 END  
    
	     


  SELECT @ErrorCode = @@ERROR
	IF (@ErrorCode <> 0)
		GOTO error
	COMMIT TRANSACTION
		SELECT @Status AS 'Status'

	ERROR:

	IF (@ErrorCode <> 0)

	BEGIN
		SELECT @ErrorCode AS 'Status'
		ROLLBACK TRANSACTION
	END

END

--select * from [gmc].[TblAppUser]


--select DATEDIFF(SECOND,  '2024-12-04 14:17:08.640',getdate())

--select getdate()
GO

IF OBJECT_ID(N'[SERP].[USP_EMP_Reports]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_EMP_Reports] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_EMP_Reports]
@EMP_ID INT = null,
@Month int=null,
@Year int=null,
@Flag varchar(100)=null

AS

BEGIN

set nocount on;
--DECLARE @EMP_ID INT = 91  
--DECLARE @Month int=10
--DECLARE @Year int=2025


IF @Flag='GetMonthRptByEMPID'
BEGIN
--Personal Dtls
 select 
  e.EMP_ID,e.EMP_Name,e.Aadhar,e.Mobile,s.EmpType,e.Gender
  --,t.ShiftTime,
  --e.ImageBase64,e.Template
  from SERP.tbl_Employee e with(nolock)
  inner join SERP.tbl_EMP_Types s  with(nolock) on s.EmpTypeID=e.EmpTypeID where e.EMP_ID=@EMP_ID
  
  --Monthly Attendance Report

SELECT 
    E.EMP_ID,
    E.EMP_Name,
    convert(varchar(10), CONVERT(DATE, A.DOA),103) AS AttendanceDate,
    CONVERT(VARCHAR(8), A.EntryTime, 108) AS InTime,   -- 108 = hh:mm:ss
    CONVERT(VARCHAR(8), A.ExitTime, 108) AS OutTime,
    S.ShiftTime,
    S.InTime AS ShiftIn,
    S.OutTime AS ShiftOut
FROM [SERP].[tbl_EMPDailyAttendance] A
INNER JOIN [SERP].[tbl_Employee] E ON A.EMP_ID = E.EMP_ID
LEFT JOIN [SERP].[tbl_Shifts] S ON ISNULL(E.ShiftID, 0) = S.ShiftID
WHERE A.EMP_ID = @EMP_ID and MONTH(A.DOA)=@Month and Year(A.DOA)=@Year
ORDER BY DOA ASC;


END
 



END
GO

IF OBJECT_ID(N'[SERP].[USP_GenerateMonthlyAttendance_Holidays]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GenerateMonthlyAttendance_Holidays] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GenerateMonthlyAttendance_Holidays]
    @Month INT,
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartDate DATE = DATEFROMPARTS(@Year,@Month,1);
    DECLARE @EndDate DATE = EOMONTH(@StartDate);

    -----------------------------------------------------------------------
    -- 1. Build Date List WITHOUT recursion (safe + fast)
    -----------------------------------------------------------------------
    ;WITH Dates AS
    (
        SELECT @StartDate AS d
        UNION ALL
        SELECT DATEADD(DAY, n.number, @StartDate)
        FROM master..spt_values n
        WHERE n.type='P' AND DATEADD(DAY, n.number, @StartDate) <= @EndDate
    ),

    ActiveEmployees AS
    (
        SELECT EMP_ID FROM SERP.tbl_Employee WHERE IsActive = 1
    ),

    AllComb AS
    (
        SELECT e.EMP_ID, d.d AS AttendanceDate
        FROM ActiveEmployees e
        CROSS JOIN Dates d
    ),

    FinalStatus AS
    (
        SELECT 
            ac.EMP_ID,
            ac.AttendanceDate,
            CASE 
                WHEN EXISTS (SELECT 1 FROM SERP.tbl_Holiday h WHERE h.HolidayDate = ac.AttendanceDate)
                    THEN 'H'
                WHEN EXISTS (SELECT 1 FROM SERP.tbl_EMPDailyAttendance a
                             WHERE a.EMP_ID = ac.EMP_ID AND CONVERT(date,a.DOA) = ac.AttendanceDate)
                    THEN 'P'
                ELSE 'A'
            END AS Status
        FROM AllComb ac
    )

    -----------------------------------------------------------------------
    -- 2. Store data into a proper Attendance Calendar table
    -----------------------------------------------------------------------
    MERGE SERP.tbl_EmpAttendance_Holiday AS T
    USING FinalStatus AS S
        ON T.EmpID = S.EMP_ID AND T.AttendanceDate = S.AttendanceDate
    WHEN MATCHED AND T.Status <> S.Status
        THEN UPDATE SET T.Status = S.Status, T.CreatedDate = GETDATE()
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (EmpID, AttendanceDate, Status, CreatedDate)
             VALUES (S.EMP_ID, S.AttendanceDate, S.Status, GETDATE());

    -----------------------------------------------------------------------
    -- 3. RETURN PIVOT TABLE to ASP.NET
    -----------------------------------------------------------------------
    DECLARE @cols NVARCHAR(MAX) = '';
    DECLARE @i INT = 1;
    DECLARE @days INT = DAY(EOMONTH(@StartDate));

    WHILE @i <= @days
    BEGIN
        SET @cols += QUOTENAME('D' + CAST(@i AS VARCHAR(2))) + ',';
        SET @i += 1;
    END

    SET @cols = LEFT(@cols, LEN(@cols)-1);

    DECLARE @sql NVARCHAR(MAX) = '
        SELECT * FROM
        (
            SELECT 
                c.EMP_ID,
                e.EMP_Name,
                ''D'' + CAST(DAY(c.AttendanceDate) AS VARCHAR(2)) AS DayCol,
                c.Status
            FROM SERP.tbl_EmployeeAttendance_Calendar c
            INNER JOIN SERP.tbl_Employee e ON e.EMP_ID = c.EMP_ID
            WHERE c.AttendanceDate BETWEEN ''' + CONVERT(VARCHAR(10),@StartDate,120) + ''' 
                                      AND ''' + CONVERT(VARCHAR(10),@EndDate,120) + '''
        ) AS src
        PIVOT
        (
            MAX(Status) FOR DayCol IN (' + @cols + ')
        ) AS pvt
        ORDER BY EMP_Name;
    ';
	------------------------------------------------------------------
	--Get number of days in month (accounting for leap years)
	------------------------------------------------------------------
	--DECLARE @Month INT = 10;  
    --DECLARE @Year INT = 2025;

-- Get number of days in month (accounting for leap years)
DECLARE @DaysInMonth INT = DAY(EOMONTH(DATEFROMPARTS(@Year, @Month, 1)));

-- If current month, limit to today's day
IF (YEAR(GETDATE()) = @Year AND MONTH(GETDATE()) = @Month)
    SET @DaysInMonth = DAY(GETDATE());

-- Create dynamic day columns
--DECLARE @cols NVARCHAR(MAX) = '';
--DECLARE @sql NVARCHAR(MAX);

-- Build day columns dynamically
SELECT @cols = STRING_AGG(QUOTENAME(CONCAT('Day', number)), ',')
FROM master..spt_values
WHERE type = 'P' AND number BETWEEN 1 AND @DaysInMonth;

-- Build and execute dynamic SQL
SET @sql = '
;WITH AttendanceData AS (
    SELECT 
        E.EMP_ID,
        E.EMP_Name,
        ''Day''+cast(DAY(Dates.CalDate) as varchar) AS DayNum,
        CASE 
            WHEN H.HolidayDate IS NOT NULL THEN H.HolidayName  
            WHEN A.EMP_ID IS NOT NULL THEN ''P''
            ELSE ''A''
        END AS Status
    FROM [SERP].[tbl_Employee] E
    CROSS JOIN (
        SELECT DATEFROMPARTS(' + CAST(@Year AS VARCHAR(4)) + ', ' + CAST(@Month AS VARCHAR(2)) + ', number) AS CalDate
        FROM master..spt_values
        WHERE type = ''P'' AND number BETWEEN 1 AND ' + CAST(@DaysInMonth AS VARCHAR(2)) + '
    ) AS Dates
    LEFT JOIN [SERP].[tbl_EMPDailyAttendance] A 
        ON E.EMP_ID = A.EMP_ID 
        AND CONVERT(DATE, A.DOA) = Dates.CalDate
    LEFT JOIN [SERP].[tbl_Holiday] H 
        ON H.HolidayDate = Dates.CalDate
)
SELECT EMP_ID, EMP_Name, ' + @cols + '
FROM AttendanceData
PIVOT
(
    MAX(Status)
    FOR DayNum IN (' + @cols + ')
) AS p
ORDER BY EMP_ID;
';

print @sql
--EXEC sp_executesql @sql;



    EXEC (@sql);
END
GO

IF OBJECT_ID(N'[SERP].[USP_GET_DetailsFor_DB]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GET_DetailsFor_DB] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GET_DetailsFor_DB]
@Flag varchar(50)=null,
@Date varchar(10) =null,
@EmpTypeID int=null

AS
BEGIN

set nocount on;

DECLARE @SelectedDate DATE = convert(date,@Date,103)

--DECLARE @SelectedDate DATE = '2025-07-26'
--EXEC SERP.USP_GET_DetailsFor_DB @Flag='DB_Counts',@Date='25/11/2025'
		IF @Flag='DB_Counts'
		BEGIN
		SELECT 
			et.EmpTypeID,
			et.EmpType AS [Section Name],
			COUNT(e.EMP_ID) AS [Staff Count],
			COUNT(DISTINCT a.EMP_ID) AS [Attended]
		FROM SERP.tbl_EMP_Types et
		LEFT JOIN SERP.tbl_Employee e  ON e.EmpTypeID = et.EmpTypeID AND e.IsActive = 1
		LEFT JOIN SERP.tbl_EMPDailyAttendance a ON a.EMP_ID = e.EMP_ID AND CONVERT(DATE, a.DOA) = @SelectedDate
		GROUP BY et.EmpTypeID,
		et.EmpType
		ORDER BY et.EmpTypeID


		END
--EXEC SERP.USP_GET_DetailsFor_DB @Flag='DB_Counts',@Date='01/12/2025';
---------------------------------------------------------------

			IF @Flag='GetEMPList'
			BEGIN

			SELECT 
				e.EMP_ID,
				e.EMP_Name,
				et.EmpType,e.Gender,e.Mobile   
    
			FROM SERP.tbl_Employee e
			INNER JOIN SERP.tbl_EMP_Types et ON et.EmpTypeID = e.EmpTypeID
			--LEFT JOIN SERP.tbl_EMPDailyAttendance a
			--    ON a.EMP_ID = e.EMP_ID
			--    AND CONVERT(DATE, a.DOA) = @SelectedDate
			WHERE 
				e.EmpTypeID = @EmpTypeID
				AND e.IsActive = 1
			ORDER BY e.EMP_Name


			END
--EXEC SERP.USP_GET_DetailsFor_DB @Flag='GetEMPList',@Date='01/12/2025';


			IF @Flag='GetEMPAttendance'
			BEGIN


			SELECT 
				e.EMP_ID,    e.EMP_Name,    et.EmpType,e.Gender,e.Mobile   ,
				a.EntryTime,a.ExitTime
			FROM SERP.tbl_Employee e
			INNER JOIN SERP.tbl_EMP_Types et ON et.EmpTypeID = e.EmpTypeID
			INNER JOIN SERP.tbl_EMPDailyAttendance a ON a.EMP_ID = e.EMP_ID AND CONVERT(DATE, a.DOA) = @SelectedDate
			WHERE e.EmpTypeID = @EmpTypeID
				  AND e.IsActive = 1
			ORDER BY a.EntryTime

			END
--EXEC SERP.USP_GET_DetailsFor_DB @Flag='GetEMPAttendance',@EmpTypeID=1;


			END
GO

IF OBJECT_ID(N'[SERP].[USP_GetEMP_AttendanceDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GetEMP_AttendanceDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GetEMP_AttendanceDtls]
@EmpTypeID int=null,
@DOA varchar(10)=null


AS

set nocount on;
BEGIN 
  select 
  e.EMP_ID,e.EMP_Name,e.Aadhar,e.Mobile,s.EmpType,e.Gender,
  --,t.ShiftTime,
  --e.ImageBase64,
  --e.Template,
  'https://api.sneha.adt-ai.com/IMSBAPI/SERPEMPImages/'+e.ImagePath+'.png' as 'ImagePath',
 convert (varchar,a.DOA,103) as 'DOA',
 RIGHT('0' + CAST(DATEPART(HOUR, DOA) AS VARCHAR), 2) + ':' +
 RIGHT('0' + CAST(DATEPART(MINUTE, DOA) AS VARCHAR), 2) as 'EntryTime',
 case when ExitTime is null then null else 
 RIGHT('0' + CAST(DATEPART(HOUR, ExitTime) AS VARCHAR), 2) + ':' +
 RIGHT('0' + CAST(DATEPART(MINUTE, ExitTime) AS VARCHAR), 2) end as 'ExitTime'

  from SERP.tbl_Employee e with(nolock)
  inner join SERP.tbl_EMP_Types s  with(nolock) on s.EmpTypeID=e.EmpTypeID
  inner join SERP.tbl_EMPDailyAttendance a on a.EMP_ID=e.EMP_ID where e.EmpTypeID=@EmpTypeID and cast(a.DOA as date)= convert(date,@DOA,103)
  order by a.DOA desc
 --*****

 select t.EmpTypeID,t.EmpType,
 ( select count(e.EMP_ID)  from SERP.tbl_Employee e with(nolock)
   where e.EmpTypeID=t.EmpTypeID ) as 'tot_EMP',
  ( select count(e.EMP_ID)  from SERP.tbl_Employee e with(nolock)  
    inner join SERP.tbl_EMPDailyAttendance a on a.EMP_ID=e.EMP_ID where e.EmpTypeID=t.EmpTypeID and cast(a.DOA as date)= convert(date,@DOA,103)) as 'tot_Attend'

 from SERP.tbl_EMP_Types t





END




--EXEC  [SERP].[USP_GetEMP_AttendanceDtls] @EmpTypeID=2,@DOA='25/07/2025'

--select * from SERP.tbl_Employee
--select * from SERP.tbl_EMPDailyAttendance where EMP_ID=13
--EXEC  [SERP].[USP_GetEMP_AttendanceDtls] @EmpTypeID=2,@DOA='17/07/2025'


-- SELECT cast ('2025-06-10 02:04:15.000' as date)
-- SELECT convert(date,'10/06/2025',103)
--SELECT CAST(GETDATE() AS TIME) AS OnlyTime
-- SELECT  convert (varchar,GETDATE() ,103) 

--SELECT 
--    RIGHT('0' + CAST(DATEPART(HOUR, GETDATE()) AS VARCHAR), 2) + ':' +
--    RIGHT('0' + CAST(DATEPART(SECOND, GETDATE()) AS VARCHAR), 2) AS Time_HH_SS
GO

IF OBJECT_ID(N'[SERP].[USP_GetEMPDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GetEMPDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GetEMPDtls]
    @Flag VARCHAR(100) = NULL,
    @SearchTerm NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default: Return all employees with extra details
    IF @Flag IS NULL
    BEGIN
        SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender,
            e.ImageBase64,
            e.Template
            --, t.ShiftTime  -- Uncomment if Shift info is needed
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
		--where 
		  --e.EmpTypeID!=1
        --LEFT JOIN SERP.tbl_Shifts t WITH (NOLOCK) ON t.ShiftID = e.ShiftID
        --INNER JOIN SERP.tbl_Designation d WITH (NOLOCK) ON d.DesigID = e.Desig  -- Uncomment if needed
        ORDER BY e.EMP_ID ASC
    END

	IF @Flag='ArtisianDetails'
	Begin
	SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender,
            e.ImageBase64,
            e.Template
            --, t.ShiftTime  -- Uncomment if Shift info is needed
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
		--where e.EmpTypeID=1
        --LEFT JOIN SERP.tbl_Shifts t WITH (NOLOCK) ON t.ShiftID = e.ShiftID
        --INNER JOIN SERP.tbl_Designation d WITH (NOLOCK) ON d.DesigID = e.Desig  -- Uncomment if needed
        ORDER BY e.EMP_ID ASC
    END



    ELSE IF @Flag = 'SearchEmpDetails'
    BEGIN
        SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
        WHERE 
            (@SearchTerm IS NULL OR
             e.EMP_Name LIKE '%' + @SearchTerm + '%' OR
             e.Mobile LIKE '%' + @SearchTerm + '%' OR
             s.EmpType LIKE '%' + @SearchTerm + '%')
			 --and e.EmpTypeID!=1
        ORDER BY e.EMP_ID ASC
    END
END
GO

IF OBJECT_ID(N'[SERP].[USP_GetEMPDtls_WebReports_Db]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GetEMPDtls_WebReports_Db] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GetEMPDtls_WebReports_Db]
    @Flag VARCHAR(100) = NULL,
    @SearchTerm NVARCHAR(100) = NULL,
	@EmpType NVARCHAR(100) = NULL,
	@EMP_ID INT = NULL,
    @EMP_Name NVARCHAR(200) = NULL,
    @Aadhar NVARCHAR(50) = NULL,
    @Mobile NVARCHAR(50) = NULL,
	@EmpTypeId INT=NULL,
    @Gender NVARCHAR(20) = NULL,
	@StallNum INT =NULL,
	@AgencyId INT = NULL,
	@Remarks NVARCHAR(200) = NULL
	--@ModifiedOn NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    -- Default: Return all employees with extra details
    IF @Flag IS NULL
    BEGIN
        SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender,
            e.ImageBase64,
            e.Template
            --, t.ShiftTime  -- Uncomment if Shift info is needed
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
		where e.EmpTypeID!=1 AND e.IsActive=1
        --LEFT JOIN SERP.tbl_Shifts t WITH (NOLOCK) ON t.ShiftID = e.ShiftID
        --INNER JOIN SERP.tbl_Designation d WITH (NOLOCK) ON d.DesigID = e.Desig  -- Uncomment if needed
        ORDER BY e.EMP_ID ASC
    END

	IF @Flag='ArtisianDetails'
	Begin
	SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender,
			e.StallNum,
			e.AgencyID,
            e.ImageBase64,
            e.Template
            --, t.ShiftTime  -- Uncomment if Shift info is needed
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
		where e.EmpTypeID=1 and IsActive=1
        --LEFT JOIN SERP.tbl_Shifts t WITH (NOLOCK) ON t.ShiftID = e.ShiftID
        --INNER JOIN SERP.tbl_Designation d WITH (NOLOCK) ON d.DesigID = e.Desig  -- Uncomment if needed
        ORDER BY e.EMP_ID ASC
    END
	--EXEC SERP.USP_GetEMPDtls_WebReports_Db @Flag='ArtisianDetails';


----------------------------
    IF @Flag = 'SearchEmpDetails'
    BEGIN
        SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
        WHERE 
            (@SearchTerm IS NULL OR
             e.EMP_Name LIKE '%' + @SearchTerm + '%' OR
             e.Mobile LIKE '%' + @SearchTerm + '%')
			 and e.EmpTypeID!=1 AND e.IsActive=1
        ORDER BY e.EMP_ID ASC
    END
	-----------------------------------------------
	 IF @Flag = 'EmpTypeList'
    BEGIN
        SELECT 
            EmpType 
        FROM SERP.tbl_EMP_Types WITH(NOLOCK)
        ORDER BY EmpType;

        RETURN;
    END
	-----------------------------
	IF @Flag = 'FilterByEmpType'
	BEGIN
	SELECT 
	       e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            t.EmpType,
            e.Gender
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types t WITH (NOLOCK)
            ON t.EmpTypeID = e.EmpTypeID
        WHERE 
            t.EmpType = @EmpType           -- filter by text
            AND e.IsActive=1
        ORDER BY e.EMP_Name;
        RETURN;
    END

	 IF @Flag='getEmpTypes'
BEGIN
     select EmpTypeID,EmpType FROM SERP.tbl_EMP_Types
	 where EmpTypeID != 1 
  END
	      
--EXEC SERP.USP_GetEMPDtls_WebReports_Db @Flag='FilterByEmpType',@EmpType='Artisans';

       IF @Flag='getEmpTypesForCalender'
BEGIN
     select EmpTypeID,EmpType FROM SERP.tbl_EMP_Types
	 ORDER BY EmpType
  END


   
    IF @Flag = 'UpdateArtisian'
    BEGIN
        UPDATE SERP.tbl_Employee
        SET EMP_Name   = @EMP_Name,
            Aadhar     = @Aadhar,
            Mobile     = @Mobile,
			EmpTypeID    = @EmpTypeID,
            Gender     = @Gender,
			StallNum   =@StallNum,
			AgencyID   =@AgencyId,
           ModifiedOn = GETDate()
           WHERE EMP_ID = @EMP_ID 
          AND EmpTypeID = 1
		  AND IsActive = 1;    -- Only Artisians not others

        RETURN;
    END
	--EXEC SERP.USP_GetEMPDtls_WebReports_Db @Flag='UpdateArtisian',@EMP_ID=21, @EMP_Name ='P.Prashanth',@Aadhar='488667260587',@Mobile ='7993629070',@EmpTypeId=1,@Gender='Male',@StallNum=6,@AgencyId=2;
	------------------------------------------------------------------------
	IF @Flag = 'SearchArtisian'
    BEGIN
        SELECT 
            e.EMP_ID,
            e.EMP_Name,
            e.Aadhar,
            e.Mobile,
            s.EmpType,
            e.Gender,
			e.StallNum,
			e.AgencyID
        FROM SERP.tbl_Employee e WITH (NOLOCK)
        INNER JOIN SERP.tbl_EMP_Types s WITH (NOLOCK) ON s.EmpTypeID = e.EmpTypeID
        WHERE 
            (@SearchTerm IS NULL OR
             e.EMP_Name LIKE '%' + @SearchTerm + '%' OR
             e.Mobile LIKE '%' + @SearchTerm + '%')
			 and e.EmpTypeID =1
			 and IsActive = 1
        ORDER BY e.EMP_ID ASC
    END


--EXEC SERP.USP_GetEMPDtls_WebReports_Db @Flag='SearchArtisian';
     
	 IF @Flag='InactiveArtisian'
	  BEGIN
       UPDATE SERP.tbl_Employee
       SET IsActive = 0,
	   Remarks = @Remarks, 
       ModifiedOn = GETDATE()
      WHERE EMP_ID = @EMP_ID and IsActive=1;
	  RETURN;
   END

		   IF @Flag = 'GetAgencyList'
		BEGIN
			SELECT AgencyID,
			Agency
			FROM SERP.tbl_ArtisansTypeAgency
			ORDER BY AgencyID ASC;
			RETURN;
		END
		--EXEC SERP.USP_GetEMPDtls_WebReports_Db @Flag='GetAgencyList'

END
GO

IF OBJECT_ID(N'[SERP].[USP_GetMonthlyAttendancePivot]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GetMonthlyAttendancePivot] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GetMonthlyAttendancePivot]
    @Month INT,
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartDate DATE = DATEFROMPARTS(@Year,@Month,1);
    DECLARE @Days INT = DAY(EOMONTH(@StartDate));
    DECLARE @EndDate DATE = EOMONTH(@StartDate);

    -- build dynamic pivot
    DECLARE @cols NVARCHAR(MAX) = '';
    DECLARE @colsSelect NVARCHAR(MAX) = '';
    DECLARE @sql NVARCHAR(MAX);

    DECLARE @i INT = 1;
    WHILE @i <= @Days
    BEGIN
        SET @cols += ', [D' + CAST(@i AS VARCHAR(2)) + ']';
        SET @colsSelect += ', COALESCE([D' + CAST(@i AS VARCHAR(2)) + '],'' '') AS [D' + CAST(@i AS VARCHAR(2)) + ']';
        SET @i = @i + 1;
    END

    SET @cols = STUFF(@cols,1,2,'');
    SET @colsSelect = STUFF(@colsSelect,1,2,'');

    SET @sql = N'
    WITH Source AS
    (
        SELECT ed.EMP_ID AS EmpID, ed.EMP_Name,
               ''D'' + CAST(DAY(ea.AttendanceDate) AS VARCHAR(2)) AS DayCol,
               ea.Status
        FROM dbo.EmployeeAttendance ea
        INNER JOIN SERP.tbl_Employee ed ON ed.EMP_ID = ea.EmpID
        WHERE ea.AttendanceDate BETWEEN @StartDate AND @EndDate
    )
    SELECT pvt.EmpID, pvt.EMP_Name' + @colsSelect + ',
           ISNULL(t.TotalP,0) AS TotalP,
           ISNULL(t.TotalA,0) AS TotalA,
           ISNULL(t.TotalH,0) AS TotalH
    FROM
    (
        SELECT EmpID, EMP_Name, DayCol, Status FROM Source
    ) src
    PIVOT
    (
        MAX(Status) FOR DayCol IN (' + @cols + ')
    ) pvt
    LEFT JOIN
    (
        SELECT EmpID,
               SUM(CASE WHEN Status=''P'' THEN 1 END) AS TotalP,
               SUM(CASE WHEN Status=''A'' THEN 1 END) AS TotalA,
               SUM(CASE WHEN Status=''H'' THEN 1 END) AS TotalH
        FROM dbo.EmployeeAttendance
        WHERE AttendanceDate BETWEEN @StartDate AND @EndDate
        GROUP BY EmpID
    ) t ON t.EmpID = pvt.EmpID
    ORDER BY pvt.EMP_Name;
    ';

    SET @sql = REPLACE(@sql, '@StartDate', '''' + CONVERT(varchar(10), @StartDate, 23) + '''');
    SET @sql = REPLACE(@sql, '@EndDate', '''' + CONVERT(varchar(10), @EndDate, 23) + '''');

    EXEC sp_executesql @sql;
END
GO

IF OBJECT_ID(N'[SERP].[USP_GetMonthlyPAResult]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_GetMonthlyPAResult] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_GetMonthlyPAResult]
    @Month INT,
    @Year INT,
	@EmpTypeID INT
AS
BEGIN
    SET NOCOUNT ON;
-- Get number of days in month (accounting for leap years)
DECLARE @DaysInMonth INT = DAY(EOMONTH(DATEFROMPARTS(@Year, @Month, 1)));
DECLARE @Today DATE = CAST(GETDATE() AS DATE);

-- If current month, limit to today's day
--=====================================================================--
--IF (YEAR(GETDATE()) = @Year AND MONTH(GETDATE()) = @Month)
  --  SET @DaysInMonth = DAY(GETDATE());
  --=================================================================--
-- Create dynamic day columns
DECLARE @cols NVARCHAR(MAX) = '';
DECLARE @sql NVARCHAR(MAX);
Declare @monthname varchar(5) 

set @monthname= case when @Month=1 then 'JAN' when @Month=2 then 'FEB' when @Month=3 then 'MAR' when @Month=4 then 'MAY' when @Month=5 then 'JUN' when @Month=7 then 'JULY' when @Month=8 then 'AUG' when @Month=9 then 'SEP' when @Month=10 then 'OCT' when @Month=11 then 'NOV' when @Month=12 then 'DEC' +' - ' END 

-- Build day columns dynamically
SELECT @cols = STRING_AGG(QUOTENAME(CONCAT('Day', number)), ',')
FROM master..spt_values
WHERE type = 'P' AND number BETWEEN 1 AND @DaysInMonth;

-- Build and execute dynamic SQL
SET @sql = '
;WITH AttendanceData AS (
           SELECT 
            E.EMP_ID,
            E.EMP_Name,
            ET.EmpType,
            ''Day'' + CAST(DAY(Dates.CalDate) AS VARCHAR) AS DayNum,
            CASE 
                WHEN Dates.CalDate > ''' + CAST(@Today AS VARCHAR(10)) + ''' THEN ''''
                WHEN H.HolidayDate IS NOT NULL THEN H.HolidayName  
                WHEN A.EMP_ID IS NOT NULL THEN ''P''
                ELSE ''A''
            END AS Status
        FROM SERP.tbl_Employee E
        INNER JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
        CROSS JOIN (
            SELECT DATEFROMPARTS(' + CAST(@Year AS VARCHAR(4)) + ', ' + CAST(@Month AS VARCHAR(2)) + ', number) AS CalDate
            FROM master..spt_values
            WHERE type = ''P'' AND number BETWEEN 1 AND ' + CAST(@DaysInMonth AS VARCHAR(2)) + '
        ) AS Dates
        LEFT JOIN SERP.tbl_EMPDailyAttendance A 
            ON E.EMP_ID = A.EMP_ID AND CONVERT(DATE, A.DOA) = Dates.CalDate
        LEFT JOIN SERP.tbl_Holiday H 
            ON H.HolidayDate = Dates.CalDate
               WHERE E.IsActive = 1
          AND (' + CAST(@EmpTypeID AS VARCHAR(10)) + ' = 0 OR E.EmpTypeID = ' + CAST(@EmpTypeID AS VARCHAR(10)) + ')
    )
    SELECT EMP_ID, EMP_Name, EmpType, ' + @cols + '
    FROM AttendanceData
    PIVOT
    (
        MAX(Status)
        FOR DayNum IN (' + @cols + ')
    ) AS p
    ORDER BY EMP_ID;
    ';

    EXEC (@sql);
END

--print @sql
--EXEC SERP.USP_GetMonthlyPAResult @Month=11,@Year=2025;
GO

IF OBJECT_ID(N'[SERP].[USP_IU_EMP_AttendanceDtls]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_IU_EMP_AttendanceDtls] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_IU_EMP_AttendanceDtls]
@EMP_ID int=null,
@AttendDatetime datetime=null

AS
BEGIN

Declare @ShiftID int
Declare @EmpTypeID int


select @ShiftID=ShiftID,@EmpTypeID=EmpTypeID from SERP.tbl_Employee where EMP_ID=@EMP_ID
print @ShiftID
print @EmpTypeID

IF EXISTS(select 1 from SERP.tbl_EMPDailyAttendance with(nolock) where EMP_ID=@EMP_ID and cast(DOA as date ) = cast(@AttendDatetime as date ))
BEGIN

 --   if(@EmpTypeID = '3' and @ShiftID = '1')
	--BEGIN
	      
	--END
	--ELSE if(@EmpTypeID = '3' and @ShiftID = '2')
	--BEGIN
	      
	--END
	--ELSE if(@EmpTypeID = '3' and @ShiftID = '3')
	--BEGIN
	      
	--END
	--ELSE if(@EmpTypeID = '4' and @ShiftID = '4')
	--BEGIN
	       
	--END
	--ELSE if(@EmpTypeID = '4' and @ShiftID = '5')
	--BEGIN
	      
	--END
	--ELSE
	--BEGIN
	      
	--END

	if EXISTS(select 1 from SERP.tbl_EMPDailyAttendance with(nolock) where EMP_ID=@EMP_ID and cast(DOA as date ) = cast(@AttendDatetime as date ) and ExitTime is not null)
	BEGIN
	    --Exited
	    select '222' as 'Status'
	END
	else
	BEGIN
	     --for exit
		 if EXISTS(select 1 from SERP.tbl_EMPDailyAttendance with(nolock) where EMP_ID=@EMP_ID and cast(DOA as date ) = cast(@AttendDatetime as date ) and ExitTime is null and DATEDIFF(MINUTE, EntryTime, getdate())>60 )
		 BEGIN
			update SERP.tbl_EMPDailyAttendance set ExitTime=@AttendDatetime where EMP_ID=@EMP_ID and cast(DOA as date ) = cast(@AttendDatetime as date ) and ExitTime is null 
		    select '333' as 'Status'
		 END
		 ELSE
		 BEGIN
		      --Attendance submitted
			  select '111' as 'Status'
		 END

	END

   
END
ELSE
BEGIN
    insert into SERP.tbl_EMPDailyAttendance(EMP_ID,DOA ,EntryTime ,CreateOn,ShiftID)values(@EMP_ID,@AttendDatetime,@AttendDatetime,GETDATE(),@ShiftID)
    select '000' as 'Status'

END

END

--EXEC [SERP].[USP_IU_EMP_AttendanceDtls] @EMP_ID=12,@AttendDatetime='2025-06-09 13:24:14.000'




--DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff")
--SELECT DATEDIFF(HOUR, '2025-05-13 22:00:00', GETDATE()) AS HoursDiff;




--CREATE Table SERP.tbl_EMPDailyAttendance(AttendanceID  int identity(1,1)  ,EMP_ID int,
--DOA datetime,EntryTime datetime,ExitTime datetime,
--CreateOn datetime,ModifiedOn datetime)
--select cast(getdate() as date )
GO

IF OBJECT_ID(N'[SERP].[USP_Maters_List]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_Maters_List] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_Maters_List]
@Flag varchar(100)=null,
@EmpTypeID int=null,
@EMP_ID int=null

AS

set nocount on;

BEGIN
IF @Flag='GetSections'
BEGIN
	
select * from SERP.tbl_Section
END
IF @Flag='GetDesignation'
BEGIN
	select DesigID,Designation from SERP.tbl_Designation where EmpTypeID=@EmpTypeID
END
IF @Flag='GetShifts'
BEGIN
    select DesigID,Designation from SERP.tbl_Designation where EmpTypeID=@EmpTypeID
	select ShiftID,ShiftTime from SERP.tbl_Shifts where EmpTypeID=@EmpTypeID
	
END
IF @Flag='GetEmpTypes'
BEGIN
	select EmpTypeID,EmpType from  SERP.tbl_EMP_TYpes
END
IF @Flag='GetArtisansTypeAgency'
BEGIN
	select AgencyID,Agency from  SERP.tbl_ArtisansTypeAgency

END
IF @Flag='GetEMpDetailsByEMPID'
BEGIN
  select 
  e.EMP_ID,e.EMP_Name,e.Aadhar,e.Mobile,s.EmpType,e.Gender,
  --,t.ShiftTime,
  e.ImageBase64,e.Template
  from SERP.tbl_Employee e with(nolock)
  inner join SERP.tbl_EMP_Types s  with(nolock) on s.EmpTypeID=e.EmpTypeID where e.EMP_ID=@EMP_ID
  
END

END
GO

IF OBJECT_ID(N'[SERP].[USP_PopulateSundaysForYear]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_PopulateSundaysForYear] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_PopulateSundaysForYear]
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @d DATE = DATEFROMPARTS(@Year,1,1);
    DECLARE @end DATE = DATEFROMPARTS(@Year,12,31);

    WHILE @d <= @end
    BEGIN
        IF DATENAME(WEEKDAY,@d) = 'Sunday'
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM SERP.tbl_Holiday WHERE HolidayDate = @d)
            BEGIN
                INSERT INTO SERP.tbl_Holiday (HolidayDate, HolidayName, IsSunday, IsGovtHoliday)
                VALUES (@d, 'Sunday', 1, 0);
            END
        END
        SET @d = DATEADD(DAY,1,@d);
    END
END
GO

IF OBJECT_ID(N'[SERP].[USP_Rpt_MonthlySalaryCalculation]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_Rpt_MonthlySalaryCalculation] AS RETURN 0;');
GO

ALTER PROCEDURE [SERP].[USP_Rpt_MonthlySalaryCalculation]
 @Month INT = null,          
 @Year INT = null,  
 @EmpTypeID INT = null


AS

BEGIN

set nocount on;
    --EXEC  SERP.USP_Rpt_MonthlySalaryCalculation  @Month=12,@Year=2025,@EmpTypeID=null
    --DECLARE @Month INT = 12;           
    --DECLARE @Year INT = 2025;
    --DECLARE @EmpTypeID INT = NULL;

    DECLARE @StartDate DATE = DATEFROMPARTS(@Year,@Month,1);
    DECLARE @EndDate DATE = EOMONTH(@StartDate);
    DECLARE @TotalDaysInMonth INT = DAY(@EndDate);

    IF @Month IS NULL OR @Year IS NULL
    BEGIN
        RAISERROR('For getMonthlyReport you must supply @Month and @Year',16,1);
        RETURN;
    END

    ;WITH AttendanceCTE AS
    (
        SELECT
            E.EMP_ID,
            E.EMP_Name,
            ET.EmpTypeID,
            ET.EmpType AS EmpType,
            D.Designation,
            D.BaseSalary,
            D.ServicePct,
            D.GSTPercent,
            @TotalDaysInMonth AS TotalDaysInMonth,
            ISNULL(COUNT(DISTINCT CONVERT(date,A.EntryTime)),0) AS TotalAttendDays
        FROM SERP.tbl_Employee E
        LEFT JOIN SERP.tbl_EMP_Types ET ON E.EmpTypeID = ET.EmpTypeID
        LEFT JOIN SERP.tbl_Designation D ON D.DesigID = E.Desig
        LEFT JOIN SERP.tbl_EMPDailyAttendance A 
            ON A.EMP_ID = E.EMP_ID
           AND CONVERT(date,A.EntryTime) BETWEEN @StartDate AND @EndDate
        WHERE
            D.DesigID IN (3,4,5,6,7)
            AND (@EmpTypeID IS NULL OR ET.EmpTypeID = @EmpTypeID)
        GROUP BY
            E.EMP_ID,E.EMP_Name,ET.EmpTypeID,ET.EmpType,
            D.Designation,D.BaseSalary,D.ServicePct,D.GSTPercent
    )

    SELECT
        EMP_ID, EMP_Name,EmpTypeID, EmpType, Designation,
        TotalDaysInMonth,TotalAttendDays,BaseSalary,       
        CAST(BaseSalary / TotalDaysInMonth AS DECIMAL(10,2)) AS PerDaySalary,
        CAST((BaseSalary / TotalDaysInMonth) * TotalAttendDays AS DECIMAL(10,2)) AS EarnedSalary,
		CAST(((BaseSalary / TotalDaysInMonth) * TotalAttendDays * ServicePct) / 100 AS DECIMAL(10,2)) AS ServiceAmount,
		CAST(
            (
                ((BaseSalary / TotalDaysInMonth) * TotalAttendDays) +
                (((BaseSalary / TotalDaysInMonth) * TotalAttendDays * ServicePct) / 100)
            ) * GSTPercent / 100
        AS DECIMAL(10,2)) AS GSTAmount,

        CAST(
            ((BaseSalary / TotalDaysInMonth) * TotalAttendDays) +
            (((BaseSalary / TotalDaysInMonth) * TotalAttendDays * ServicePct) / 100) +
            (
                (
                    ((BaseSalary / TotalDaysInMonth) * TotalAttendDays) +
                    (((BaseSalary / TotalDaysInMonth) * TotalAttendDays * ServicePct) / 100)
                ) * GSTPercent / 100
            )
        AS DECIMAL(10,2)) AS TotalSalary

    FROM AttendanceCTE
    ORDER BY EMP_ID

END
GO

IF OBJECT_ID(N'[SERP].[USP_UserLogin]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [SERP].[USP_UserLogin] AS RETURN 0;');
GO

--select * from SERP.TblUser

ALTER PROCEDURE [SERP].[USP_UserLogin]

@UserName varchar(10),
@Password nvarchar(500)

As
BEGIN

set nocount on;

IF EXISTS(select 1 from SERP.TblUser with(nolock) where USerName=@UserName and Password=@Password and ISActive=1)
   BEGIN
      select '000' as 'Status',UserID,FullName,Mobile,UserType ,Role from SERP.TblUser with(nolock) where USerName=@UserName and Password=@Password and ISActive=1
   END
ELSE
BEGIN
     select '111' as 'Status',0 as 'UserID','' as 'FullName','' as Mobile ,0 as 'UserType','' as Role
    
END

END
GO
