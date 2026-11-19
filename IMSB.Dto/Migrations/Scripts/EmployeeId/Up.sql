-- Migration RemoveMandalsAddEmployeeId: employee codes "EMP001", "EMP002", ... in SERP.tbl_Employee.EmployeeId.
--   * existing employees are numbered in EMP_ID order (after any code already given)
--   * new numbers come from the sequence SERP.EmployeeIdSeq, so a deleted employee's code is never given again:
--     the Staff page (IMSB.Backend StaffDetails) takes the next number itself; the trigger SERP.SetEmployeeId numbers rows
--     added without a code (the SERP app's SERP.USP_EMP_Registration), so the SERP code and procedures need no change
--   * a code is unique (filtered unique index; rows still without one are allowed)
--   * dbo.vw_StaffEmployees shows the code as Staff.EmployeeId (attendance, leaves, OT, payroll, search, dashboard)
-- Each batch is safe to re-run.

IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'EmployeeId') IS NULL
    ALTER TABLE [SERP].[tbl_Employee] ADD [EmployeeId] varchar(20) NULL;
GO

DECLARE @Max bigint = ISNULL((SELECT MAX(TRY_CAST(SUBSTRING([EmployeeId], 4, 17) AS bigint))
                              FROM [SERP].[tbl_Employee] WHERE [EmployeeId] LIKE 'EMP[0-9]%'), 0);
WITH Numbered AS
(
    SELECT [EmployeeId], ROW_NUMBER() OVER (ORDER BY [EMP_ID]) AS rn
    FROM [SERP].[tbl_Employee]
    WHERE [EmployeeId] IS NULL
)
UPDATE Numbered SET [EmployeeId] = 'EMP' + FORMAT(@Max + rn, '000');
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_tbl_Employee_EmployeeId' AND object_id = OBJECT_ID(N'[SERP].[tbl_Employee]'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_tbl_Employee_EmployeeId]
        ON [SERP].[tbl_Employee] ([EmployeeId]) WHERE [EmployeeId] IS NOT NULL;
GO

-- the next number after the highest code on record
IF OBJECT_ID(N'[SERP].[EmployeeIdSeq]', N'SO') IS NULL
BEGIN
    DECLARE @Start bigint = 1 + ISNULL((SELECT MAX(TRY_CAST(SUBSTRING([EmployeeId], 4, 17) AS bigint))
                                        FROM [SERP].[tbl_Employee] WHERE [EmployeeId] LIKE 'EMP[0-9]%'), 0);
    DECLARE @Sql nvarchar(400) = N'CREATE SEQUENCE [SERP].[EmployeeIdSeq] AS bigint START WITH ' + CAST(@Start AS nvarchar(20))
                               + N' INCREMENT BY 1 MINVALUE 1 NO CYCLE NO CACHE;';
    EXEC sp_executesql @Sql;
END
GO

-- rows added without a code (the SERP app's USP_EMP_Registration) get the next one; rows the Staff page adds already have one
CREATE OR ALTER TRIGGER [SERP].[SetEmployeeId]
ON [SERP].[tbl_Employee]
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;   -- USP_EMP_Registration returns its own status row; the trigger must not add row counts

    DECLARE @Count bigint = (SELECT COUNT(*) FROM inserted WHERE [EmployeeId] IS NULL);
    IF @Count = 0
        RETURN;

    -- one block of numbers for the whole insert, handed out in EMP_ID order
    DECLARE @First sql_variant;
    EXEC sys.sp_sequence_get_range @sequence_name = N'[SERP].[EmployeeIdSeq]', @range_size = @Count, @range_first_value = @First OUTPUT;

    WITH Numbered AS
    (
        SELECT e.[EmployeeId], ROW_NUMBER() OVER (ORDER BY e.[EMP_ID]) AS rn
        FROM [SERP].[tbl_Employee] e
        JOIN inserted i ON i.[EMP_ID] = e.[EMP_ID]
        WHERE e.[EmployeeId] IS NULL
    )
    UPDATE Numbered SET [EmployeeId] = 'EMP' + FORMAT(CAST(@First AS bigint) + rn - 1, '000');
END
GO

-- as created by EmployeesReplaceStaff, with EmployeeId now the employee code (the EMP_ID as text only for a row without one)
CREATE OR ALTER VIEW [dbo].[vw_StaffEmployees] AS
SELECT
    e.[EMP_ID] AS [Id],
    CAST(COALESCE(e.[EmployeeId], CAST(e.[EMP_ID] AS varchar(20))) AS nvarchar(20)) AS [EmployeeId],
    CAST(ISNULL(e.[EMP_Name], N'') AS nvarchar(max)) AS [FullName],
    CAST(ISNULL(e.[Mobile], N'') AS nvarchar(32)) AS [Mobile],
    CAST(NULL AS nvarchar(200)) AS [Email],
    CAST(NULL AS nvarchar(64)) AS [AadhaarHash],
    CAST(CASE WHEN LEN(e.[Aadhar]) >= 4 THEN RIGHT(e.[Aadhar], 4) END AS nvarchar(4)) AS [AadhaarLast4],
    CAST('19000101' AS datetime2) AS [DateOfBirth],
    CAST(ISNULL(e.[Gender], N'') AS nvarchar(max)) AS [Gender],
    ISNULL(e.[DepartmentID], 0) AS [DepartmentId],
    CAST(NULL AS nvarchar(20)) AS [EmployeeType],
    CAST(ISNULL(d.[Designation], N'') AS nvarchar(max)) AS [Designation],
    CAST(ISNULL(dep.[Name], N'') AS nvarchar(100)) AS [StaffCategory],
    CAST(ISNULL(d.[BaseSalary], 0) AS decimal(18,2)) AS [MonthlySalary],
    CAST(COALESCE(e.[JoinDate], CAST(e.[CreateOn] AS date), CAST('19000101' AS date)) AS datetime2) AS [JoinDate],
    CAST(ISNULL(sh.[Code], N'') AS nvarchar(max)) AS [Shift],
    CAST(CASE WHEN e.[IsActive] = 1 THEN N'ACTIVE' ELSE N'INACTIVE' END AS nvarchar(max)) AS [Status],
    CAST(ISNULL(e.[CreateOn], '19000101') AS datetime2) AS [CreatedAt],
    CAST(e.[ModifiedOn] AS datetime2) AS [UpdatedAt],
    CAST(NULL AS nvarchar(max)) AS [CreatedBy],
    CAST(NULL AS nvarchar(max)) AS [UpdatedBy],
    CAST(NULL AS datetime2) AS [ContractEndDate]
FROM [SERP].[tbl_Employee] e
OUTER APPLY (
    SELECT TOP (1) x.[Designation], x.[BaseSalary]
    FROM [SERP].[tbl_Designation] x
    WHERE x.[DesigID] = e.[Desig]
    ORDER BY CASE WHEN x.[EmpId] = e.[EMP_ID] THEN 0 ELSE 1 END
) d
LEFT JOIN [dbo].[departments] dep ON dep.[Id] = e.[DepartmentID]
LEFT JOIN [dbo].[Shift] sh ON sh.[Id] = e.[ShiftID];
