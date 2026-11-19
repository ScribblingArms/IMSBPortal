-- Rolls back the employee codes of RemoveMandalsAddEmployeeId (EmployeeId/Up.sql): the view shows the EMP_ID as text again,
-- then the trigger, sequence, index and column go (the codes themselves are lost). Each batch is safe to re-run.

CREATE OR ALTER VIEW [dbo].[vw_StaffEmployees] AS
SELECT
    e.[EMP_ID] AS [Id],
    CAST(e.[EMP_ID] AS nvarchar(20)) AS [EmployeeId],
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
GO

DROP TRIGGER IF EXISTS [SERP].[SetEmployeeId];
GO

DROP SEQUENCE IF EXISTS [SERP].[EmployeeIdSeq];
GO

DROP INDEX IF EXISTS [UX_tbl_Employee_EmployeeId] ON [SERP].[tbl_Employee];
GO

IF COL_LENGTH(N'[SERP].[tbl_Employee]', N'EmployeeId') IS NOT NULL
    ALTER TABLE [SERP].[tbl_Employee] DROP COLUMN [EmployeeId];
