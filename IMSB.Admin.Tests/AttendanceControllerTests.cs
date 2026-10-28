using System.Security.Claims;
using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>Fixtures for the attendance tests: seeded staff, a wired controller and sheet builders.</summary>
internal static class AttendanceTestHelpers
{
    /// <summary>A past day the sheet can be saved for.</summary>
    public static readonly DateTime Day = DateTime.Today.AddDays(-1);

    /// <summary>
    /// Departments Sales / Admin and shifts MORNING (8AM-4PM, 8h) / EVENING (4PM-10PM, 6h) from TestHelpers, plus
    /// EMP001 Meena Kumari (Sales, MORNING, "Active"), EMP002 Ravi Shankar (Admin, "evening"), EMP003 Old Timer (INACTIVE),
    /// EMP004 Leela Devi (Admin, ON_LEAVE) and EMP005 New Joiner (Sales, joins today).
    /// </summary>
    public static IMSBContext SeededContext()
    {
        var ctx = TestHelpers.CreateContext();
        var joiner = TestHelpers.ExistingStaff("EMP005", "New Joiner", "900000005");
        joiner.JoinDate = DateTime.Today;
        ctx.Staff.AddRange(
            TestHelpers.ExistingStaff("EMP001", "Meena Kumari", "900000001", status: "Active", dept: TestHelpers.SalesDept, shift: "MORNING"),
            TestHelpers.ExistingStaff("EMP002", "Ravi Shankar", "900000002", dept: TestHelpers.AdminDept, shift: "evening"),
            TestHelpers.ExistingStaff("EMP003", "Old Timer", "900000003", status: "INACTIVE"),
            TestHelpers.ExistingStaff("EMP004", "Leela Devi", "900000004", status: "ON_LEAVE", dept: TestHelpers.AdminDept),
            joiner);
        ctx.SaveChanges();
        return ctx;
    }

    public static AttendanceController Create(IMSBContext ctx, string userName = "admin@imsb") =>
        new(new IMSB.Backend.Attendance(ctx), ctx, TestHelpers.LocalizerFactory())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, "Staff") }, "Test"))
                }
            }
        };

    public static AttendanceEntryDto E(string employeeId, string status, string? inTime = null, string? outTime = null, string? remarks = null) =>
        new() { EmployeeId = employeeId, Status = status, InTime = inTime, OutTime = outTime, Remarks = remarks };

    public static AttendanceSheetDto Sheet(DateTime day, params AttendanceEntryDto[] entries) =>
        new() { Date = day.ToString("yyyy-MM-dd"), Entries = entries.ToList() };

    /// <summary>EMP001 present 08:00-18:00 (10h, 2h overtime) and EMP002 absent on <see cref="Day"/>.</summary>
    public static async Task SaveDefaultDay(AttendanceController c)
    {
        var result = await c.Save(Sheet(Day, E("EMP001", "PRESENT", "08:00", "18:00"), E("EMP002", "ABSENT")));
        Assert.IsType<OkObjectResult>(result);
    }

    public static string RecordId(IMSBContext ctx, string employeeId, DateTime day) =>
        ctx.AttendanceRecords.Single(r => r.Staff!.EmployeeId == employeeId && r.AttendanceDate == day).Id.ToString();
}

public class AttendanceRulesTests
{
    [Theory]
    [InlineData("Morning (8AM-4PM)", "08:00", "16:00", 8)]
    [InlineData("Afternoon (12PM-8PM)", "12:00", "20:00", 8)]
    [InlineData("Evening (4PM-10PM)", "16:00", "22:00", 6)]
    [InlineData("Night (10PM-6AM)", "22:00", "06:00", 8)]
    [InlineData("Split 9:30am – 1:30pm", "09:30", "13:30", 4)]
    public void Shift_times_and_length_are_read_from_the_shift_name(string name, string start, string end, decimal hours)
    {
        var t = AttendanceRules.ShiftTimes(name);
        Assert.NotNull(t);
        Assert.Equal(TimeSpan.Parse(start), t.Value.Start);
        Assert.Equal(TimeSpan.Parse(end), t.Value.End);
        Assert.Equal(hours, AttendanceRules.ShiftHours(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("General")]
    [InlineData("13PM-4PM")]
    public void Shift_without_readable_times_uses_the_default_length(string? name)
    {
        Assert.Null(AttendanceRules.ShiftTimes(name));
        Assert.Equal(AttendanceRules.DefaultShiftHours, AttendanceRules.ShiftHours(name));
    }

    [Theory]
    [InlineData("PRESENT", "09:00", "19:00", 8, 10, 2)]
    [InlineData("PRESENT", "09:00", "17:00", 8, 8, 0)]
    [InlineData("PRESENT", "09:00", "15:30", 8, 6.5, 0)]
    [InlineData("PRESENT", "16:00", "23:30", 6, 7.5, 1.5)]
    [InlineData("HALF_DAY", "09:00", "14:00", 4, 5, 0)]     // half days earn no overtime
    [InlineData("PRESENT", "09:00", null, 8, 0, 0)]          // still in
    [InlineData("PRESENT", "18:00", "09:00", 8, 0, 0)]       // out before in
    [InlineData("ABSENT", "09:00", "17:00", 8, 0, 0)]
    [InlineData("LEAVE", null, null, 8, 0, 0)]
    public void Hours_and_overtime(string status, string? inTime, string? outTime, decimal shift, decimal worked, decimal overtime)
    {
        TimeSpan? Parse(string? s) => s == null ? null : TimeSpan.Parse(s);
        var h = AttendanceRules.Hours(status, Parse(inTime), Parse(outTime), shift);
        Assert.Equal(worked, h.Worked);
        Assert.Equal(overtime, h.Overtime);
    }

    [Theory]
    [InlineData(3, 1, 5, 70.0)]
    [InlineData(4, 0, 4, 100.0)]
    [InlineData(0, 0, 3, 0.0)]
    [InlineData(1, 0, 3, 33.3)]
    public void Percent_counts_half_days_as_half(int present, int halfDay, int marked, decimal expected)
    {
        Assert.Equal(expected, AttendanceRules.Percent(present, halfDay, marked));
    }

    [Fact]
    public void Percent_is_null_when_nothing_is_marked()
    {
        Assert.Null(AttendanceRules.Percent(0, 0, 0));
    }

    [Theory]
    [InlineData("present", "PRESENT")]
    [InlineData(" Absent ", "ABSENT")]
    [InlineData("leave", "LEAVE")]
    [InlineData("Half Day", "HALF_DAY")]
    [InlineData("half-day", "HALF_DAY")]
    [InlineData("late", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Status_normalization(string? input, string? expected)
    {
        Assert.Equal(expected, AttendanceStatuses.Normalize(input));
    }
}

public class AttendanceControllerTests
{
    private static readonly DateTime Day = AttendanceTestHelpers.Day;

    private static async Task<LeaveBalanceRow> Balance(IMSBContext context, string employeeId = "EMP001")
    {
        var result = Assert.IsType<ViewResult>(await new LeavesController(context, TestHelpers.LocalizerFactory()).Index());
        var model = Assert.IsType<LeaveManagementViewModel>(result.Model);
        return Assert.Single(model.Balances.Where(row => row.EmployeeId == employeeId));
    }

    // ── Save (Attendance Entry) ───────────────────────────────────

    [Fact]
    public async Task Save_creates_records_with_hours_and_overtime_from_the_staff_shift()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx, "clerk@imsb");

        var result = await c.Save(AttendanceTestHelpers.Sheet(Day,
            AttendanceTestHelpers.E("EMP001", "PRESENT", "08:00", "18:30"),         // MORNING 8h: 10.5h, 2.5h OT
            AttendanceTestHelpers.E("emp002", "present", "16:00", "23:30"),         // "evening" → EVENING 6h: 7.5h, 1.5h OT
            AttendanceTestHelpers.E("EMP004", "LEAVE", "09:00", "17:00", " sick ")  // times are not kept for leave
        ));

        var json = TestHelpers.OkJson(result);
        Assert.Equal(3, json.GetProperty("saved").GetInt32());
        Assert.Equal(3, json.GetProperty("created").GetInt32());
        Assert.Equal(0, json.GetProperty("updated").GetInt32());

        var meena = ctx.AttendanceRecords.Single(r => r.Staff!.EmployeeId == "EMP001");
        Assert.Equal(Day, meena.AttendanceDate);
        Assert.Equal(AttendanceStatuses.Present, meena.Status);
        Assert.Equal(new TimeSpan(8, 0, 0), meena.InTime);
        Assert.Equal(10.5m, meena.WorkedHours);
        Assert.Equal(2.5m, meena.OvertimeHours);
        Assert.Equal("clerk@imsb", meena.CreatedBy);

        var ravi = ctx.AttendanceRecords.Single(r => r.Staff!.EmployeeId == "EMP002");
        Assert.Equal(7.5m, ravi.WorkedHours);
        Assert.Equal(1.5m, ravi.OvertimeHours);

        var leela = ctx.AttendanceRecords.Single(r => r.Staff!.EmployeeId == "EMP004");
        Assert.Equal(AttendanceStatuses.Leave, leela.Status);
        Assert.Null(leela.InTime);
        Assert.Null(leela.OutTime);
        Assert.Equal(0m, leela.WorkedHours);
        Assert.Equal("sick", leela.Remarks);
    }

    [Fact]
    public async Task Saving_the_same_day_again_updates_instead_of_duplicating()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        await AttendanceTestHelpers.SaveDefaultDay(AttendanceTestHelpers.Create(ctx, "first@imsb"));

        var result = await AttendanceTestHelpers.Create(ctx, "second@imsb").Save(AttendanceTestHelpers.Sheet(Day,
            AttendanceTestHelpers.E("EMP001", "HALF_DAY", "08:00", "12:00"),
            AttendanceTestHelpers.E("EMP004", "PRESENT", "08:00", "16:00")));

        var json = TestHelpers.OkJson(result);
        Assert.Equal(1, json.GetProperty("created").GetInt32());
        Assert.Equal(1, json.GetProperty("updated").GetInt32());
        Assert.Equal(3, ctx.AttendanceRecords.Count());
        var meena = Assert.Single(ctx.AttendanceRecords.Where(r => r.Staff!.EmployeeId == "EMP001"));
        Assert.Equal(AttendanceStatuses.HalfDay, meena.Status);
        Assert.Equal(4m, meena.WorkedHours);
        Assert.Equal(0m, meena.OvertimeHours);
        Assert.Equal("first@imsb", meena.CreatedBy);
        Assert.Equal("second@imsb", meena.UpdatedBy);
        Assert.NotNull(meena.UpdatedAt);
    }

    [Fact]
    public async Task Attendance_leave_balance_tracks_status_edits_deduplicates_and_marks_uncovered_days_lop()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var attendanceDay = DateTime.Today;
        var employee = ctx.Staff.Single(person => person.EmployeeId == "EMP001");
        employee.StaffCategory = "Operators";
        var secondEmployee = ctx.Staff.Single(person => person.EmployeeId == "EMP002");
        secondEmployee.StaffCategory = "Clerks";
        ctx.LeaveEntitlements.Add(new LeaveEntitlement
        {
            StaffId = employee.Id,
            StaffCategory = "Operators",
            LeaveType = "Casual",
            Year = attendanceDay.Year,
            AnnualQuota = 1m,
            IsAttendanceLeaveType = true
        });
        ctx.LeaveEntitlements.Add(new LeaveEntitlement
        {
            StaffId = secondEmployee.Id,
            StaffCategory = "Clerks",
            LeaveType = "Annual",
            Year = attendanceDay.Year,
            AnnualQuota = 0.5m,
            IsAttendanceLeaveType = true
        });
        await ctx.SaveChangesAsync();
        var attendance = AttendanceTestHelpers.Create(ctx);

        Assert.IsType<OkObjectResult>(await attendance.Save(AttendanceTestHelpers.Sheet(attendanceDay,
            AttendanceTestHelpers.E("EMP001", "LEAVE"),
            AttendanceTestHelpers.E("EMP002", "LEAVE"))));
        Assert.Equal(1m, (await Balance(ctx)).Taken);
        Assert.Equal(0m, (await Balance(ctx)).Balance);
        Assert.True(ctx.AttendanceRecords.Single(record => record.StaffId == secondEmployee.Id).LeaveWithoutPay);
        Assert.Equal(0m, (await Balance(ctx, "EMP002")).Taken);

        Assert.IsType<OkObjectResult>(await attendance.Save(AttendanceTestHelpers.Sheet(attendanceDay,
            AttendanceTestHelpers.E("EMP001", "LEAVE"),
            AttendanceTestHelpers.E("EMP002", "LEAVE"))));
        Assert.Single(ctx.AttendanceRecords.Where(record => record.StaffId == employee.Id));
        Assert.Equal(1m, (await Balance(ctx)).Taken);

        var leaveId = AttendanceTestHelpers.RecordId(ctx, "EMP001", attendanceDay);
        TestHelpers.OkJson(await attendance.Update(leaveId,
            new AttendanceUpdateDto { Status = "PRESENT", InTime = "08:00" }));
        Assert.Equal(0m, (await Balance(ctx)).Taken);
        Assert.Equal(1m, (await Balance(ctx)).Balance);

        TestHelpers.OkJson(await attendance.Update(leaveId,
            new AttendanceUpdateDto { Status = "HALF_DAY", InTime = "08:00", OutTime = "12:00" }));
        Assert.Equal(0.5m, (await Balance(ctx)).Taken);
        Assert.Equal(0.5m, (await Balance(ctx)).Balance);

        TestHelpers.OkJson(await attendance.Update(leaveId,
            new AttendanceUpdateDto { Status = "LEAVE" }));
        Assert.Equal(1m, (await Balance(ctx)).Taken);
        Assert.Equal(0m, (await Balance(ctx)).Balance);

        var secondId = AttendanceTestHelpers.RecordId(ctx, "EMP002", attendanceDay);
        TestHelpers.OkJson(await attendance.Update(secondId,
            new AttendanceUpdateDto { Status = "HALF_DAY", InTime = "08:00", OutTime = "12:00" }));
        Assert.Equal(0.5m, (await Balance(ctx, "EMP002")).Taken);
        TestHelpers.OkJson(await attendance.Update(secondId,
            new AttendanceUpdateDto { Status = "LEAVE" }));
        Assert.True(ctx.AttendanceRecords.Single(record => record.Id == Guid.Parse(secondId)).LeaveWithoutPay);
        Assert.Equal(0m, (await Balance(ctx, "EMP002")).Taken);
        TestHelpers.OkJson(await attendance.Update(secondId,
            new AttendanceUpdateDto { Status = "ABSENT" }));
        Assert.False(ctx.AttendanceRecords.Single(record => record.Id == Guid.Parse(secondId)).LeaveWithoutPay);
        Assert.Equal(0m, (await Balance(ctx, "EMP002")).Taken);
    }

    [Fact]
    public async Task Present_without_an_out_time_is_saved_with_no_hours_yet()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var result = await AttendanceTestHelpers.Create(ctx).Save(AttendanceTestHelpers.Sheet(Day, AttendanceTestHelpers.E("EMP001", "PRESENT", "08:00")));

        Assert.IsType<OkObjectResult>(result);
        var r = ctx.AttendanceRecords.Single();
        Assert.Null(r.OutTime);
        Assert.Equal(0m, r.WorkedHours);
    }

    [Fact]
    public async Task Save_rejects_bad_dates()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);

        var future = TestHelpers.Errors(await c.Save(AttendanceTestHelpers.Sheet(DateTime.Today.AddDays(1), AttendanceTestHelpers.E("EMP001", "ABSENT"))));
        Assert.Equal("Date cannot be in the future", future["date"]);

        var bad = TestHelpers.Errors(await c.Save(new AttendanceSheetDto { Date = "09/10/2026", Entries = { AttendanceTestHelpers.E("EMP001", "ABSENT") } }));
        Assert.Equal("Invalid date", bad["date"]);
        Assert.Empty(ctx.AttendanceRecords);
    }

    [Fact]
    public async Task Save_with_no_entries_says_there_is_nothing_to_save()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var errors = TestHelpers.Errors(await AttendanceTestHelpers.Create(ctx).Save(AttendanceTestHelpers.Sheet(Day)));
        Assert.StartsWith("Nothing to save", errors["entries"]);
    }

    [Fact]
    public async Task Save_rejects_more_entries_than_the_limit()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var entries = Enumerable.Range(0, AttendanceController.MaxEntriesPerSave + 1).Select(_ => AttendanceTestHelpers.E("EMP001", "ABSENT")).ToArray();
        var errors = TestHelpers.Errors(await AttendanceTestHelpers.Create(ctx).Save(AttendanceTestHelpers.Sheet(Day, entries)));
        Assert.Equal($"At most {AttendanceController.MaxEntriesPerSave} entries can be saved at once", errors["entries"]);
    }

    [Fact]
    public async Task Save_reports_each_row_problem_under_its_index_and_saves_nothing()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var errors = TestHelpers.Errors(await AttendanceTestHelpers.Create(ctx).Save(AttendanceTestHelpers.Sheet(Day,
            AttendanceTestHelpers.E("EMP999", "PRESENT", "09:00"),            // 0 unknown staff
            AttendanceTestHelpers.E("EMP001", "PRESENT"),                     // 1 no in-time
            AttendanceTestHelpers.E("EMP001", "ABSENT"),                      // 2 same staff twice
            AttendanceTestHelpers.E("EMP002", "PRESENT", "18:00", "09:00"),   // 3 out before in
            AttendanceTestHelpers.E("EMP004", "LATE"),                        // 4 unknown status
            AttendanceTestHelpers.E("EMP005", "PRESENT", "09:00", "17:00"),   // 5 joins today, sheet is yesterday
            AttendanceTestHelpers.E("EMP003", "HALF_DAY", "25:00")            // 6 not a time
        )));

        Assert.Equal("Staff member not found", errors["entries_0_employee_id"]);
        Assert.Equal("Enter the in-time", errors["entries_1_in_time"]);
        Assert.Equal("This staff member appears more than once", errors["entries_2_employee_id"]);
        Assert.Equal("Out-time must be after the in-time", errors["entries_3_out_time"]);
        Assert.Equal("Select Present, Absent, Leave or Half Day", errors["entries_4_status"]);
        Assert.Equal("This staff member had not joined on this date", errors["entries_5_employee_id"]);
        Assert.Equal("Enter a valid time (HH:mm)", errors["entries_6_in_time"]);
        Assert.Empty(ctx.AttendanceRecords);
    }

    [Theory]
    [InlineData("ta-IN", "வெளியே நேரம் உள்ளே நேரத்திற்குப் பிறகு இருக்க வேண்டும்")]
    [InlineData("te-IN", "అవుట్-టైమ్ ఇన్-టైమ్ తర్వాత ఉండాలి")]
    [InlineData("hi-IN", "आउट-टाइम, इन-टाइम के बाद होना चाहिए")]
    public async Task Validation_messages_follow_the_request_culture(string culture, string expected)
    {
        using var scope = new TestHelpers.CultureScope(culture);
        using var ctx = AttendanceTestHelpers.SeededContext();
        var errors = TestHelpers.Errors(await AttendanceTestHelpers.Create(ctx).Save(AttendanceTestHelpers.Sheet(Day,
            AttendanceTestHelpers.E("EMP001", "PRESENT", "18:00", "09:00"))));
        Assert.Equal(expected, errors["entries_0_out_time"]);
    }

    // ── Data (sheet, stats, week, departments) ────────────────────

    [Fact]
    public async Task Sheet_lists_employed_staff_that_are_not_inactive()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var json = TestHelpers.OkJson(await AttendanceTestHelpers.Create(ctx).Data(Day.ToString("yyyy-MM-dd"), null));

        // ordered by department, then name; EMP003 is inactive and EMP005 had not joined yet
        var ids = json.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("employeeId").GetString()).ToList();
        Assert.Equal(new[] { "EMP004", "EMP002", "EMP001" }, ids);

        var meena = json.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetString() == "EMP001");
        Assert.Equal("Sales", meena.GetProperty("departmentName").GetString());
        Assert.Equal("Morning (8AM-4PM)", meena.GetProperty("shift").GetString());
        Assert.Equal("08:00", meena.GetProperty("shiftStart").GetString());
        Assert.Equal("16:00", meena.GetProperty("shiftEnd").GetString());
        Assert.Equal(8m, meena.GetProperty("shiftHours").GetDecimal());
        Assert.Equal("ACTIVE", meena.GetProperty("staffStatus").GetString());
        Assert.Equal(JsonValueKind.Null, meena.GetProperty("record").ValueKind);
    }

    [Fact]
    public async Task Sheet_includes_anyone_already_marked_that_day()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var oldTimer = ctx.Staff.Single(s => s.EmployeeId == "EMP003");
        ctx.AttendanceRecords.Add(new AttendanceRecord { StaffId = oldTimer.Id, AttendanceDate = Day, Status = AttendanceStatuses.Absent });
        ctx.SaveChanges();

        var json = TestHelpers.OkJson(await AttendanceTestHelpers.Create(ctx).Data(Day.ToString("yyyy-MM-dd"), null));

        Assert.Contains(json.GetProperty("rows").EnumerateArray(), r => r.GetProperty("employeeId").GetString() == "EMP003");
    }

    [Fact]
    public async Task Data_returns_saved_records_and_day_counts()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var json = TestHelpers.OkJson(await c.Data(Day.ToString("yyyy-MM-dd"), null));

        Assert.Equal(Day.ToString("yyyy-MM-dd"), json.GetProperty("date").GetString());
        Assert.False(json.GetProperty("isFuture").GetBoolean());
        var stats = json.GetProperty("stats");
        Assert.Equal(3, stats.GetProperty("total").GetInt32());
        Assert.Equal(1, stats.GetProperty("present").GetInt32());
        Assert.Equal(1, stats.GetProperty("absent").GetInt32());
        Assert.Equal(0, stats.GetProperty("leave").GetInt32());
        Assert.Equal(1, stats.GetProperty("notMarked").GetInt32());
        Assert.Equal(1, stats.GetProperty("withOvertime").GetInt32());
        Assert.Equal(50.0m, stats.GetProperty("percent").GetDecimal());

        var record = json.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeeId").GetString() == "EMP001").GetProperty("record");
        Assert.Equal("PRESENT", record.GetProperty("status").GetString());
        Assert.Equal("08:00", record.GetProperty("inTime").GetString());
        Assert.Equal("18:00", record.GetProperty("outTime").GetString());
        Assert.Equal(10m, record.GetProperty("workedHours").GetDecimal());
        Assert.Equal(2m, record.GetProperty("overtimeHours").GetDecimal());
        Assert.Equal("admin@imsb", record.GetProperty("by").GetString());
    }

    [Fact]
    public async Task Monthly_calendar_returns_all_days_and_attendance_for_the_selected_month()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);
        ctx.Staff.Single(s => s.EmployeeId == "EMP002").Designation = "Security";
        ctx.SaveChanges();
        var inactive = ctx.Staff.Single(s => s.EmployeeId == "EMP003");
        ctx.AttendanceRecords.Add(new AttendanceRecord
        {
            StaffId = inactive.Id,
            AttendanceDate = Day,
            Status = AttendanceStatuses.Absent
        });
        ctx.SaveChanges();

        var json = TestHelpers.OkJson(await c.Monthly(Day.ToString("yyyy-MM"), null));
        var days = json.GetProperty("days").EnumerateArray().Select(d => d.GetInt32()).ToList();
        var rows = json.GetProperty("rows").EnumerateArray().ToList();

        Assert.Equal(DateTime.DaysInMonth(Day.Year, Day.Month), days.Count);
        Assert.Equal(5, rows.Count); // active staff plus inactive staff with a record in this month
        var meena = rows.Single(r => r.GetProperty("employeeId").GetString() == "EMP001");
        Assert.Equal(AttendanceStatuses.Present, meena.GetProperty("days")[Day.Day - 1].GetString());
        var ravi = rows.Single(r => r.GetProperty("employeeId").GetString() == "EMP002");
        Assert.Equal(AttendanceStatuses.Absent, ravi.GetProperty("days")[Day.Day - 1].GetString());
        var oldTimer = rows.Single(r => r.GetProperty("employeeId").GetString() == "EMP003");
        Assert.Equal(AttendanceStatuses.Absent, oldTimer.GetProperty("days")[Day.Day - 1].GetString());
        Assert.Equal(JsonValueKind.Null, oldTimer.GetProperty("days")[Day.Day - 2].ValueKind);

        var filtered = TestHelpers.OkJson(await c.Monthly(Day.ToString("yyyy-MM"), "executive"));
        Assert.Equal(4, filtered.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task Monthly_calendar_rejects_invalid_month_and_exports_filtered_csv()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        ctx.Staff.Single(s => s.EmployeeId == "EMP002").Designation = "Security";
        ctx.SaveChanges();

        Assert.Equal("Invalid date", TestHelpers.Errors(await c.Monthly("2026-13", null))["month"]);

        var result = await c.MonthlyExport(Day.ToString("yyyy-MM"), "Executive");
        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal($"attendance_calendar_{Day:yyyy-MM}.xls", file.FileDownloadName);
        var lines = SalesTestHelpers.CsvText(result).TrimEnd().Split(Environment.NewLine);
        Assert.StartsWith("Employee ID,Staff Name,Employee Type,1,2", lines[0]);
        Assert.Equal(4, lines.Length); // three eligible staff of this type plus the header
    }

    [Fact]
    public async Task Monthly_calendar_search_and_status_filters_match_the_export()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var report = TestHelpers.OkJson(await c.Monthly(Day.ToString("yyyy-MM"), null, "meena", "present"));
        var rows = report.GetProperty("rows").EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal("EMP001", rows[0].GetProperty("employeeId").GetString());

        var export = await c.MonthlyExport(Day.ToString("yyyy-MM"), null, "meena", "PRESENT");
        var lines = SalesTestHelpers.CsvText(export).TrimEnd().Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.Contains("EMP001", lines[1]);
        Assert.DoesNotContain("EMP002", string.Join(Environment.NewLine, lines));
    }

    [Fact]
    public async Task Monthly_calendar_status_filter_includes_not_marked_and_rejects_unknown_status()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var unmarked = TestHelpers.OkJson(await c.Monthly(Day.ToString("yyyy-MM"), null, null, "not_marked"));
        Assert.Equal(4, unmarked.GetProperty("rows").GetArrayLength());

        var invalid = Assert.IsType<BadRequestObjectResult>(
            await c.Monthly(Day.ToString("yyyy-MM"), null, null, "UNKNOWN"));
        Assert.Equal("Select a valid monthly attendance status", TestHelpers.Errors(invalid)["status"]);
    }

    [Fact]
    public async Task Department_filter_limits_the_rows_but_not_the_day_figures()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var json = TestHelpers.OkJson(await AttendanceTestHelpers.Create(ctx).Data(Day.ToString("yyyy-MM-dd"), TestHelpers.AdminDept.ToString()));

        Assert.Equal(new[] { "EMP004", "EMP002" }, json.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("employeeId").GetString()));
        Assert.Equal(3, json.GetProperty("stats").GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("departments").GetArrayLength());
    }

    [Fact]
    public async Task Departments_have_day_counts_and_month_to_date_rate()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var json = TestHelpers.OkJson(await c.Data(Day.ToString("yyyy-MM-dd"), null));
        var depts = json.GetProperty("departments").EnumerateArray().ToDictionary(d => d.GetProperty("name").GetString()!);

        var sales = depts["Sales"];
        Assert.Equal(1, sales.GetProperty("total").GetInt32());
        Assert.Equal(1, sales.GetProperty("present").GetInt32());
        Assert.Equal(100.0m, sales.GetProperty("percent").GetDecimal());
        Assert.Equal(100.0m, sales.GetProperty("monthPercent").GetDecimal());

        var admin = depts["Admin"];
        Assert.Equal(2, admin.GetProperty("total").GetInt32());
        Assert.Equal(1, admin.GetProperty("absent").GetInt32());
        Assert.Equal(1, admin.GetProperty("notMarked").GetInt32());
        Assert.Equal(0.0m, admin.GetProperty("percent").GetDecimal());
        Assert.Equal(TestHelpers.AdminDept.ToString(), admin.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Week_runs_monday_to_sunday_with_each_days_rate()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var week = TestHelpers.OkJson(await c.Data(Day.ToString("yyyy-MM-dd"), null)).GetProperty("week");

        var monday = Day.AddDays(-(((int)Day.DayOfWeek + 6) % 7));
        Assert.Equal(monday.ToString("yyyy-MM-dd"), week.GetProperty("start").GetString());
        Assert.Equal(monday.AddDays(6).ToString("yyyy-MM-dd"), week.GetProperty("end").GetString());
        var days = week.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        var selected = days.Single(d => d.GetProperty("date").GetString() == Day.ToString("yyyy-MM-dd"));
        Assert.Equal(2, selected.GetProperty("marked").GetInt32());
        Assert.Equal(50.0m, selected.GetProperty("percent").GetDecimal());
        Assert.False(selected.GetProperty("future").GetBoolean());
        Assert.All(days.Where(d => d.GetProperty("marked").GetInt32() == 0), d => Assert.Equal(JsonValueKind.Null, d.GetProperty("percent").ValueKind));
        Assert.All(days, d => Assert.Equal(DateTime.Parse(d.GetProperty("date").GetString()!) > DateTime.Today, d.GetProperty("future").GetBoolean()));
    }

    [Fact]
    public async Task Data_defaults_to_today_and_rejects_a_bad_date()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);

        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), TestHelpers.OkJson(await c.Data(null, null)).GetProperty("date").GetString());
        Assert.Equal("Invalid date", TestHelpers.Errors(await c.Data("2026-13-01", null))["date"]);
    }

    // ── Get / Update / Delete ─────────────────────────────────────

    [Fact]
    public async Task Get_returns_the_record_with_staff_details()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);

        var json = TestHelpers.OkJson(await c.Get(AttendanceTestHelpers.RecordId(ctx, "EMP001", Day)));

        Assert.Equal("EMP001", json.GetProperty("employeeId").GetString());
        Assert.Equal("Meena Kumari", json.GetProperty("name").GetString());
        Assert.Equal("Sales", json.GetProperty("departmentName").GetString());
        Assert.Equal(Day.ToString("yyyy-MM-dd"), json.GetProperty("date").GetString());
        Assert.Equal("PRESENT", json.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("4f8a1c56-0000-0000-0000-000000000000")]
    public async Task Unknown_ids_are_not_found(string id)
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);

        Assert.IsType<NotFoundResult>(await c.Get(id));
        Assert.IsType<NotFoundResult>(await c.Update(id, new AttendanceUpdateDto { Status = "ABSENT" }));
        Assert.IsType<NotFoundResult>(await c.Delete(id));
    }

    [Fact]
    public async Task Update_changes_one_record_and_recalculates_hours()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        await AttendanceTestHelpers.SaveDefaultDay(AttendanceTestHelpers.Create(ctx, "first@imsb"));
        var id = AttendanceTestHelpers.RecordId(ctx, "EMP001", Day);

        var json = TestHelpers.OkJson(await AttendanceTestHelpers.Create(ctx, "editor@imsb").Update(id,
            new AttendanceUpdateDto { Status = "PRESENT", InTime = "07:30", OutTime = "19:00", Remarks = "stock count" }));

        Assert.Equal(11.5m, json.GetProperty("workedHours").GetDecimal());
        Assert.Equal(3.5m, json.GetProperty("overtimeHours").GetDecimal());
        Assert.Equal("editor@imsb", json.GetProperty("by").GetString());
        var r = ctx.AttendanceRecords.Single(x => x.Id == Guid.Parse(id));
        Assert.Equal("stock count", r.Remarks);
        Assert.Equal(new TimeSpan(7, 30, 0), r.InTime);
    }

    [Fact]
    public async Task Update_to_leave_clears_the_times()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);
        var id = AttendanceTestHelpers.RecordId(ctx, "EMP001", Day);

        TestHelpers.OkJson(await c.Update(id, new AttendanceUpdateDto { Status = "leave", InTime = "08:00", OutTime = "18:00" }));

        var r = ctx.AttendanceRecords.Single(x => x.Id == Guid.Parse(id));
        Assert.Equal(AttendanceStatuses.Leave, r.Status);
        Assert.Null(r.InTime);
        Assert.Null(r.OutTime);
        Assert.Equal(0m, r.WorkedHours);
        Assert.Equal(0m, r.OvertimeHours);
    }

    [Fact]
    public async Task Update_validates_status_and_times()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);
        var id = AttendanceTestHelpers.RecordId(ctx, "EMP001", Day);

        Assert.Equal("Select Present, Absent, Leave or Half Day", TestHelpers.Errors(await c.Update(id, new AttendanceUpdateDto { Status = "late" }))["status"]);
        Assert.Equal("Enter the in-time", TestHelpers.Errors(await c.Update(id, new AttendanceUpdateDto { Status = "PRESENT" }))["in_time"]);
        Assert.Equal("Out-time must be after the in-time",
            TestHelpers.Errors(await c.Update(id, new AttendanceUpdateDto { Status = "HALF_DAY", InTime = "13:00", OutTime = "13:00" }))["out_time"]);

        // unchanged
        Assert.Equal(10m, ctx.AttendanceRecords.Single(x => x.Id == Guid.Parse(id)).WorkedHours);
    }

    [Fact]
    public async Task Delete_removes_only_that_record()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);
        var id = AttendanceTestHelpers.RecordId(ctx, "EMP001", Day);

        var json = TestHelpers.OkJson(await c.Delete(id));

        Assert.Equal("EMP001", json.GetProperty("employeeId").GetString());
        Assert.Single(ctx.AttendanceRecords);
        Assert.Equal("EMP002", ctx.AttendanceRecords.Single().Staff!.EmployeeId);
        Assert.IsType<NotFoundResult>(await c.Delete(id));
    }

    // ── History / Export ──────────────────────────────────────────

    /// <summary>Day: EMP001 present, EMP002 absent; 40 days earlier (an earlier month): EMP004 on leave.</summary>
    private static async Task<(IMSBContext Ctx, AttendanceController C)> HistoryFixture()
    {
        var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        await AttendanceTestHelpers.SaveDefaultDay(c);
        Assert.IsType<OkObjectResult>(await c.Save(AttendanceTestHelpers.Sheet(Day.AddDays(-40), AttendanceTestHelpers.E("EMP004", "LEAVE"))));
        return (ctx, c);
    }

    private static List<string> HistoryIds(IActionResult result) =>
        TestHelpers.OkJson(result).GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("employeeId").GetString()!).ToList();

    [Fact]
    public async Task History_defaults_to_the_month_and_has_totals()
    {
        var (ctx, c) = await HistoryFixture();
        using var _ = ctx;

        var json = TestHelpers.OkJson(await c.History(null, null, null, Day.ToString("yyyy-MM"), null, null));

        Assert.Equal(new DateTime(Day.Year, Day.Month, 1).ToString("yyyy-MM-dd"), json.GetProperty("from").GetString());
        var rows = json.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(new[] { "EMP001", "EMP002" }, rows.Select(r => r.GetProperty("employeeId").GetString()));   // same date: by name
        var totals = json.GetProperty("totals");
        Assert.Equal(2, totals.GetProperty("records").GetInt32());
        Assert.Equal(1, totals.GetProperty("present").GetInt32());
        Assert.Equal(1, totals.GetProperty("absent").GetInt32());
        Assert.Equal(10m, totals.GetProperty("hours").GetDecimal());
        Assert.Equal(2m, totals.GetProperty("overtime").GetDecimal());
        Assert.Equal(50.0m, totals.GetProperty("percent").GetDecimal());
    }

    [Fact]
    public async Task History_filters_work()
    {
        var (ctx, c) = await HistoryFixture();
        using var _ = ctx;
        string from = Day.AddDays(-40).ToString("yyyy-MM-dd"), to = Day.ToString("yyyy-MM-dd");

        Assert.Equal(new[] { "EMP001", "EMP002", "EMP004" }, HistoryIds(await c.History(null, null, null, null, from, to)));   // newest date first
        Assert.Equal(new[] { "EMP002" }, HistoryIds(await c.History("RAVI", null, null, null, from, to)));
        Assert.Equal(new[] { "EMP001" }, HistoryIds(await c.History("emp001", null, null, null, from, to)));
        Assert.Equal(new[] { "EMP002", "EMP004" }, HistoryIds(await c.History(null, TestHelpers.AdminDept.ToString(), null, null, from, to)));
        Assert.Equal(new[] { "EMP004" }, HistoryIds(await c.History(null, null, "leave", null, from, to)));
        Assert.Equal(new[] { "EMP002" }, HistoryIds(await c.History(null, TestHelpers.AdminDept.ToString(), "ABSENT", null, from, to)));
        Assert.Empty(HistoryIds(await c.History("nobody", null, null, null, from, to)));
        // one end of the range alone means that single day
        Assert.Equal(new[] { "EMP004" }, HistoryIds(await c.History(null, null, null, null, from, null)));
        // an unknown status or department id is ignored rather than matching nothing
        Assert.Equal(3, HistoryIds(await c.History(null, "x", "LATE", null, from, to)).Count);
    }

    [Fact]
    public async Task History_rejects_bad_periods()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);

        Assert.Equal("The end date must be on or after the start date", TestHelpers.Errors(await c.History(null, null, null, null, "2026-05-10", "2026-05-01"))["to"]);
        Assert.Equal("Invalid date", TestHelpers.Errors(await c.History(null, null, null, null, "10-05-2026", null))["from"]);
        Assert.Equal("Invalid date", TestHelpers.Errors(await c.History(null, null, null, "2026-13", null, null))["month"]);
        Assert.Equal("Invalid date", TestHelpers.Errors(await c.Export(null, null, null, "May", null, null))["month"]);
    }

    [Fact]
    public async Task Export_writes_a_localized_csv_with_totals()
    {
        var (ctx, c) = await HistoryFixture();
        using var _ = ctx;
        var day = Day.ToString("yyyy-MM-dd");

        var result = await c.Export(null, null, null, null, day, day);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal($"attendance_{Day:yyyyMMdd}_{Day:yyyyMMdd}.xls", file.FileDownloadName);
        var lines = SalesTestHelpers.CsvText(result).TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Date,Employee ID,Staff Name,Department,Designation,In-Time,Out-Time,Hours,OT Hrs,Status,Remarks,By", lines[0]);
        Assert.Equal($"{day},EMP002,Ravi Shankar,Admin,Executive,,,0,0,Absent,,admin@imsb", lines[1]);     // by department, then name
        Assert.Equal($"{day},EMP001,Meena Kumari,Sales,Executive,08:00,18:00,10,2,Present,,admin@imsb", lines[2]);
        Assert.Equal("Total,,,,,,,10,2,,,", lines[3]);
        Assert.Equal(4, lines.Length);
    }

    [Fact]
    public async Task Export_follows_the_culture_and_neutralizes_formulas()
    {
        using var ctx = AttendanceTestHelpers.SeededContext();
        var c = AttendanceTestHelpers.Create(ctx);
        Assert.IsType<OkObjectResult>(await c.Save(AttendanceTestHelpers.Sheet(Day, AttendanceTestHelpers.E("EMP004", "HALF_DAY", "09:00", "13:00", "=HYPERLINK(\"x\")"))));

        using var scope = new TestHelpers.CultureScope("hi-IN");
        var csv = SalesTestHelpers.CsvText(await c.Export(null, null, null, Day.ToString("yyyy-MM"), null, null));

        Assert.StartsWith("तारीख,", csv);
        Assert.Contains(",आधा दिन,", csv);
        Assert.Contains("\"=HYPERLINK(\"\"x\"\")\"", csv);
    }

    [Theory]
    [InlineData("en-US", "Half Day")]
    [InlineData("ta-IN", "அரை நாள்")]
    [InlineData("te-IN", "అర్ధ రోజు")]
    [InlineData("hi-IN", "आधा दिन")]
    public void Status_labels_are_localized(string culture, string halfDay)
    {
        using var scope = new TestHelpers.CultureScope(culture);
        var L = TestHelpers.SharedLocalizer();
        Assert.Equal(halfDay, AttendanceController.StatusLabel(L, AttendanceStatuses.HalfDay));
        Assert.Equal(L["Present"].Value, AttendanceController.StatusLabel(L, AttendanceStatuses.Present));
    }
}
