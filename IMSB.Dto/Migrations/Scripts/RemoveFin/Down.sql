-- Reverts RemoveFinSchemaObjects: re-creates the FIN schema, its 3 tables (empty) and 2 procedures,
-- copied from the FIN batches of GmcDb/Schemas, Tables, Defaults and Procedures.sql. Each batch is safe to re-run.

IF SCHEMA_ID(N'FIN') IS NULL
    EXEC(N'CREATE SCHEMA [FIN]');
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
