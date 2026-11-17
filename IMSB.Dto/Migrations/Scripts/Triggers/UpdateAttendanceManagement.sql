-- Trigger UpdateAttendanceManagement, current version (migration AttendanceRecordsCreatedAtFromPunch). Earlier versions, kept
-- for the migrations that ran them: UpdateAttendanceManagement.v1.sql (Guid ids), .v2.sql (int ids, staff matched by
-- Aadhaar/mobile), .v3.sql (StaffId = EMP_ID, CreatedAt = trigger time).
-- Whenever SERP.tbl_EMPDailyAttendance rows are inserted or updated (face-recognition punches: entry, then exit),
-- the employee's dbo.attendance_records row for that day is inserted, or updated when it exists.
--
-- attendance_records.StaffId is the employee's SERP.tbl_Employee.EMP_ID (FK_attendance_records_tbl_Employee_StaffId,
-- migration EmployeesReplaceStaff): every punch of an employee in tbl_Employee is recorded, no staff lookup.
-- attendance_records.CreatedAt is the punch's tbl_EMPDailyAttendance.CreateOn (the day's first CreateOn when the employee
-- punched more than once), kept the same on every recalculation; the trigger time only when CreateOn is empty.
--
-- Status from the day's first entry and last exit, against the staff member's shift length:
--   no entry                         ABSENT
--   entry, no exit yet               PRESENT (still at work; recalculated when the exit is punched)
--   worked >= 75% of the shift       PRESENT   (overtime = worked - shift)
--   worked >= 50% of the shift       HALF_DAY
--   worked <  50% of the shift       ABSENT
-- Shift length: the employee's shift, tbl_Employee.ShiftID in dbo.Shift (as dbo.vw_StaffEmployees reads it;
-- "06:00 AM to 02:00 PM", as AttendanceRules.ShiftHours reads it), else the SERP shift's InTime/Outtime,
-- else 8 hours (AttendanceRules.DefaultShiftHours).

-- one employee's punches for one day, read by the trigger (and SERP.USP_IU_EMP_AttendanceDtls) instead of a table scan
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_tbl_EMPDailyAttendance_EMP_ID_DOA' AND object_id = OBJECT_ID(N'[SERP].[tbl_EMPDailyAttendance]'))
    CREATE NONCLUSTERED INDEX [IX_tbl_EMPDailyAttendance_EMP_ID_DOA]
        ON [SERP].[tbl_EMPDailyAttendance] ([EMP_ID], [DOA])
        INCLUDE ([EntryTime], [ExitTime], [ShiftID]);
GO

CREATE OR ALTER TRIGGER [SERP].[UpdateAttendanceManagement]
ON [SERP].[tbl_EMPDailyAttendance]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;   -- the SERP procedures return their own status row; the trigger must not add row counts

    -- nothing to do: no rows, or an UPDATE that left the punch columns alone (UPDATE() is true for every column on INSERT)
    IF NOT EXISTS (SELECT 1 FROM inserted)
       OR NOT (UPDATE(EMP_ID) OR UPDATE(DOA) OR UPDATE(EntryTime) OR UPDATE(ExitTime) OR UPDATE(ShiftID) OR UPDATE(CreateOn))
        RETURN;

    DECLARE @FullDayShare      decimal(4,2)  = 0.75;
    DECLARE @HalfDayShare      decimal(4,2)  = 0.50;
    DECLARE @DefaultShiftMins  int           = 8 * 60;
    DECLARE @By                nvarchar(100) = N'SERP attendance';
    DECLARE @Now               datetime2     = SYSUTCDATETIME();   -- UpdatedAt, and CreatedAt when the punch has no CreateOn

    DECLARE @Calc TABLE
    (
        StaffId        int              NOT NULL,
        AttendanceDate date             NOT NULL,
        Status         nvarchar(20)     NOT NULL,
        InTime         time             NULL,
        OutTime        time             NULL,
        WorkedHours    decimal(5,2)     NOT NULL,
        OvertimeHours  decimal(5,2)     NOT NULL,
        CreatedAt      datetime2        NOT NULL,   -- the punch's CreateOn
        PRIMARY KEY (StaffId, AttendanceDate)
    );

    WITH Touched AS
    (
        -- each employee/day changed; the whole day is recalculated from all of its punches, not only the changed row
        SELECT DISTINCT i.EMP_ID, CAST(ISNULL(i.DOA, i.EntryTime) AS date) AS AttendanceDate
        FROM inserted i
        WHERE i.EMP_ID IS NOT NULL AND ISNULL(i.DOA, i.EntryTime) IS NOT NULL
    ),
    Emp AS
    (
        -- the employee (StaffId = EMP_ID) and shift length, worked out once per employee (not per punch);
        -- the JOIN keeps out punches of an EMP_ID that is not in tbl_Employee (the FK would reject them)
        SELECT e.EMP_ID, e.ShiftID, e.EMP_ID AS StaffId, own.Mins AS OwnShiftMins
        FROM (SELECT DISTINCT EMP_ID FROM Touched) t
        JOIN [SERP].[tbl_Employee] e ON e.EMP_ID = t.EMP_ID
        -- employee shift: dbo.Shift name "06:00 AM to 02:00 PM"; overnight shifts wrap past midnight (1..1440 minutes);
        -- no dbo.Shift row: NULL, and the SERP shift or the default is used
        OUTER APPLY (SELECT TOP (1) sh.Name FROM dbo.Shift sh WHERE sh.Id = e.ShiftID) shift
        CROSS APPLY (SELECT TRY_CONVERT(time, LEFT(LTRIM(shift.Name), 8))  AS StartT,
                            TRY_CONVERT(time, RIGHT(RTRIM(shift.Name), 8)) AS EndT) x
        CROSS APPLY (SELECT (DATEDIFF(minute, x.StartT, x.EndT) + 1439) % 1440 + 1 AS Mins) own
    ),
    Days AS
    (
        -- earliest entry / latest exit per employee and day (an employee may punch more than once a day);
        -- ISNULL/NULLIF around the aggregates: no "Null value is eliminated" warning reaches the SERP app
        SELECT m.StaffId, t.AttendanceDate,
               NULLIF(MIN(ISNULL(a.EntryTime, '99991231')), '99991231') AS EntryTime,
               NULLIF(MAX(ISNULL(a.ExitTime,  '17530101')), '17530101') AS ExitTime,
               NULLIF(MIN(ISNULL(a.CreateOn,  '99991231')), '99991231') AS CreateOn,
               MAX(COALESCE(m.OwnShiftMins, serp.Mins, @DefaultShiftMins)) AS ShiftMins
        FROM Touched t
        JOIN Emp m ON m.EMP_ID = t.EMP_ID
        -- seek on IX_tbl_EMPDailyAttendance_EMP_ID_DOA
        JOIN [SERP].[tbl_EMPDailyAttendance] a
          ON a.EMP_ID = t.EMP_ID
         AND (    (a.DOA >= t.AttendanceDate AND a.DOA < DATEADD(day, 1, t.AttendanceDate))
               OR (a.DOA IS NULL AND a.EntryTime >= t.AttendanceDate AND a.EntryTime < DATEADD(day, 1, t.AttendanceDate)))
        OUTER APPLY (SELECT TOP (1) (DATEDIFF(minute, ss.InTime, ss.Outtime) + 1439) % 1440 + 1 AS Mins
                     FROM [SERP].[tbl_Shifts] ss
                     WHERE m.OwnShiftMins IS NULL                    -- only needed when the staff shift could not be read
                       AND ss.ShiftID = ISNULL(a.ShiftID, m.ShiftID) AND ss.InTime IS NOT NULL AND ss.Outtime IS NOT NULL) serp
        GROUP BY m.StaffId, t.AttendanceDate
    )
    INSERT INTO @Calc (StaffId, AttendanceDate, Status, InTime, OutTime, WorkedHours, OvertimeHours, CreatedAt)
    SELECT d.StaffId, d.AttendanceDate, s.Status,
           CAST(d.EntryTime AS time), CAST(d.ExitTime AS time),
           CAST(ROUND(w.Mins / 60.0, 2) AS decimal(5,2)),
           CASE WHEN s.Status = N'PRESENT' AND w.Mins > d.ShiftMins
                THEN CAST(ROUND((w.Mins - d.ShiftMins) / 60.0, 2) AS decimal(5,2)) ELSE 0 END,
           ISNULL(CAST(d.CreateOn AS datetime2), @Now)
    FROM Days d
    CROSS APPLY (SELECT CASE WHEN d.ExitTime > d.EntryTime THEN DATEDIFF(minute, d.EntryTime, d.ExitTime) ELSE 0 END AS Mins) w
    CROSS APPLY (SELECT CASE WHEN d.EntryTime IS NULL THEN N'ABSENT'
                             WHEN d.ExitTime IS NULL OR d.ExitTime <= d.EntryTime THEN N'PRESENT'
                             WHEN w.Mins >= d.ShiftMins * @FullDayShare THEN N'PRESENT'
                             WHEN w.Mins >= d.ShiftMins * @HalfDayShare THEN N'HALF_DAY'
                             ELSE N'ABSENT' END AS Status) s;

    IF @@ROWCOUNT = 0 RETURN;

    -- update: the day is already in attendance_records (rows whose values would not change are left alone)
    UPDATE r
    SET r.Status          = c.Status,
        r.InTime          = c.InTime,
        r.OutTime         = c.OutTime,
        r.WorkedHours     = c.WorkedHours,
        r.OvertimeHours   = c.OvertimeHours,
        -- ABSENT is always unpaid in payroll; a half day keeps the leave-policy result the app worked out
        r.LeaveWithoutPay = CASE WHEN c.Status = N'HALF_DAY' THEN r.LeaveWithoutPay ELSE 0 END,
        r.CreatedAt       = c.CreatedAt,   -- same as the punch's CreateOn
        r.UpdatedAt       = @Now,
        r.UpdatedBy       = @By
    FROM dbo.attendance_records r
    JOIN @Calc c ON c.StaffId = r.StaffId AND c.AttendanceDate = r.AttendanceDate
    WHERE NOT EXISTS (SELECT c.Status, c.InTime, c.OutTime, c.WorkedHours, c.OvertimeHours,
                             CASE WHEN c.Status = N'HALF_DAY' THEN r.LeaveWithoutPay ELSE 0 END, c.CreatedAt
                      INTERSECT
                      SELECT r.Status, r.InTime, r.OutTime, r.WorkedHours, r.OvertimeHours, r.LeaveWithoutPay, r.CreatedAt);

    -- insert: the first punch of the day; CreatedAt = the punch's CreateOn
    INSERT INTO dbo.attendance_records
        (StaffId, AttendanceDate, Status, LeaveWithoutPay, InTime, OutTime, WorkedHours, OvertimeHours, Remarks, CreatedAt, CreatedBy)   -- Id: IDENTITY
    SELECT c.StaffId, c.AttendanceDate, c.Status, 0, c.InTime, c.OutTime, c.WorkedHours, c.OvertimeHours, NULL, c.CreatedAt, @By
    FROM @Calc c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.attendance_records r WITH (UPDLOCK, HOLDLOCK)
                      WHERE r.StaffId = c.StaffId AND r.AttendanceDate = c.AttendanceDate);
END
