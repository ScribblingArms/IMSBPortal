using System.Security.Claims;
using IMSB.Models;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

public class PayrollControllerTests
{
    // ── LOP days: only Absent, unpaid leave, LOP leave entries and days before joining ──

    private static readonly DateTime Nov = new(2026, 11, 1);   // 30 days

    private static AttendanceRecord MarkOn(int day, string status, bool withoutPay = false) =>
        new() { AttendanceDate = Nov.AddDays(day - 1), Status = status, LeaveWithoutPay = withoutPay };

    [Fact]
    public void Lop_days_count_absent_and_unpaid_leave_and_unmarked_days_are_payable()
    {
        var records = new[]
        {
            MarkOn(1, AttendanceStatuses.Present),
            MarkOn(2, AttendanceStatuses.HalfDay),                        // other half on paid leave
            MarkOn(3, AttendanceStatuses.Leave),                          // paid leave
            MarkOn(4, AttendanceStatuses.Leave, withoutPay: true),        // 1
            MarkOn(5, AttendanceStatuses.Absent),                         // 1
            MarkOn(6, AttendanceStatuses.HalfDay, withoutPay: true)       // 0.5
        };

        Assert.Equal(2.5m, IMSB.Backend.Payroll.LopDays(Nov, new DateTime(2020, 1, 1), records, Array.Empty<StaffLeaveEntry>()));
    }

    [Fact]
    public void Lop_days_with_no_attendance_marked_are_zero()
    {
        Assert.Equal(0m, IMSB.Backend.Payroll.LopDays(Nov, new DateTime(2020, 1, 1),
            Array.Empty<AttendanceRecord>(), Array.Empty<StaffLeaveEntry>()));
    }

    [Fact]
    public void Lop_leave_entries_count_on_unmarked_days_only_once_and_other_types_do_not_count()
    {
        var entries = new[]
        {
            new StaffLeaveEntry { LeaveType = "LOP", FromDate = Nov.AddDays(9), ToDate = Nov.AddDays(11) },      // 10th–12th
            new StaffLeaveEntry { LeaveType = "Casual", FromDate = Nov.AddDays(19), ToDate = Nov.AddDays(20) }  // not LOP
        };
        // the 11th is also marked Leave without pay: counted once
        var records = new[] { MarkOn(11, AttendanceStatuses.Leave, withoutPay: true) };

        Assert.Equal(3m, IMSB.Backend.Payroll.LopDays(Nov, new DateTime(2020, 1, 1), records, entries));
    }

    [Fact]
    public void Lop_leave_entries_from_last_month_count_only_their_days_in_this_month()
    {
        var entries = new[] { new StaffLeaveEntry { LeaveType = "LOP", FromDate = Nov.AddDays(-3), ToDate = Nov.AddDays(1) } };

        Assert.Equal(2m, IMSB.Backend.Payroll.LopDays(Nov, new DateTime(2020, 1, 1), Array.Empty<AttendanceRecord>(), entries));
    }

    [Fact]
    public void Days_before_joining_are_not_payable()
    {
        Assert.Equal(10m, IMSB.Backend.Payroll.LopDays(Nov, Nov.AddDays(10), Array.Empty<AttendanceRecord>(), Array.Empty<StaffLeaveEntry>()));
    }

    [Theory]
    // salary, LOP days, OT → LOP deduction, net (30-day month)
    [InlineData(10000, 0, 100, 0, 10100)]
    [InlineData(10000, 3, 205.42, 1000, 9205.42)]
    [InlineData(10000, 30, 100, 10000, 100)]
    [InlineData(31000, 2.5, 0, 2583.33, 28416.67)]
    public void Net_pay_is_reduced_by_the_salary_of_the_lop_days(decimal salary, decimal lopDays, decimal ot, decimal lop, decimal net)
    {
        var basicPay = IMSB.Backend.Payroll.ProratedPay(salary, 30 - lopDays, 30);
        var row = new IMSB.Interfaces.PayBillRow(999999, "E", "N", "D", "X", 30, 30 - lopDays, salary, basicPay, 0, 1.5m, ot, 0,
            IMSB.Backend.Payroll.NetPay(salary, ot, basicPay));

        Assert.Equal((lopDays, lop, net), (row.LopDays, row.LopDeduction, row.NetPay));
    }

    [Fact]
    public async Task Statement_row_shows_payable_days_lop_days_and_ot_hours_from_lop_leave()
    {
        using var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("9101", "Lop Test", "9000000191");
        staff.MonthlySalary = 3000m;
        staff.JoinDate = new DateTime(2020, 1, 1);
        context.AddEmployees(staff);
        context.AttendanceRecords.Add(new AttendanceRecord { StaffId = staff.Id, AttendanceDate = Nov.AddDays(1), Status = AttendanceStatuses.Absent });
        context.StaffLeaveEntries.Add(new StaffLeaveEntry
        {
            StaffId = staff.Id, LeaveType = "LOP", Year = 2026, FromDate = Nov.AddDays(4), ToDate = Nov.AddDays(5), Days = 2m
        });
        await context.SaveChangesAsync();

        var row = (await new IMSB.Backend.Payroll(context).RowsAsync(Nov)).Single(r => r.EmployeeId == "9101");

        // 1 absent + 2 LOP leave = 3 LOP days of 30; ₹100 a day
        Assert.Equal((27m, 3m, 300m, 2700m), (row.PaidDays, row.LopDays, row.LopDeduction, row.NetPay));
        var json = System.Text.Json.JsonSerializer.Serialize(row, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"lopDays\":3", json);
        Assert.Contains("\"overtimeHours\":", json);
    }

    [Fact]
    public void Statement_screen_and_exports_show_payable_days_lop_days_and_ot_hours()
    {
        var dir = typeof(PayrollControllerTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!;
        var index = File.ReadAllText(Path.Combine(dir, "Views", "Payroll", "Index.cshtml"));
        var js = File.ReadAllText(Path.Combine(dir, "wwwroot", "js", "payroll.js"));
        var pdf = File.ReadAllText(Path.Combine(dir, "Views", "Payroll", "_PayBillDocument.cshtml"));

        Assert.Contains("<th>@L[\"PayableDays\"]</th><th>@L[\"MonthlySalary\"]</th><th>@L[\"OTHours\"]</th><th>@L[\"OvertimeAmount\"]</th>"
            + "<th>@L[\"LOPDays\"]</th><th>@L[\"LOPDeduction\"]</th>", index);
        Assert.Contains("<th>@L[\"LOPDays\"]</th><th>@L[\"LOPDeduction\"]</th>", pdf);
        // the cells follow the headers: LOP days straight before LOP deduction
        Assert.True(js.IndexOf("row.lopDays", StringComparison.Ordinal) < js.IndexOf("row.lopDeduction", StringComparison.Ordinal));
        Assert.True(js.IndexOf("row.overtimeAmount", StringComparison.Ordinal) < js.IndexOf("row.lopDays", StringComparison.Ordinal));
        Assert.DoesNotContain("@L[\"PaidDays\"]", index);
        Assert.Contains("row.lopDays", js);
        Assert.Contains("row.overtimeHours", js);
        Assert.Contains("@L[\"PayableDays\"]", pdf);
        Assert.Contains("@L[\"LOPDays\"]", pdf);
    }

    [Fact]
    public async Task Generate_saves_prorated_pay_adjustments_and_employee_history()
    {
        using var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("9201", "Payroll Test", "9000000091");
        staff.MonthlySalary = 3100m;
        context.AddEmployees(staff);
        var start = new DateTime(2026, 10, 1);
        context.AttendanceRecords.AddRange(
            new AttendanceRecord { StaffId = staff.Id, AttendanceDate = start, Status = AttendanceStatuses.Present, OvertimeHours = 1.5m },
            new AttendanceRecord { StaffId = staff.Id, AttendanceDate = start.AddDays(1), Status = AttendanceStatuses.HalfDay, OvertimeHours = 1m },
            new AttendanceRecord { StaffId = staff.Id, AttendanceDate = start.AddDays(2), Status = AttendanceStatuses.Leave },
            new AttendanceRecord { StaffId = staff.Id, AttendanceDate = start.AddDays(3), Status = AttendanceStatuses.Leave, LeaveWithoutPay = true });
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id,
            Year = start.AddMonths(-1).Year,
            Month = start.AddMonths(-1).Month,
            EmployeeId = staff.EmployeeId,
            EmployeeName = staff.FullName,
            MonthDays = DateTime.DaysInMonth(start.AddMonths(-1).Year, start.AddMonths(-1).Month),
            PaidDays = 1,
            MonthlySalary = staff.MonthlySalary,
            BasicPay = 100m,
            NetPay = 100m
        });
        await context.SaveChangesAsync();

        Assert.True(await new IMSB.Backend.Payroll(context).GenerateAsync(start,
            new[] { new PayBillAdjustmentDto { StaffId = staff.Id, Allowances = 100m, OvertimeAmount = 30m, Deductions = 20m } }));

        var saved =Assert.Single(context.PayBillEntries.Where(entry => entry.Year == start.Year && entry.Month == start.Month));
        // only the Leave without pay is LOP (the other 27 unmarked days are payable): 30 of 31 days
        Assert.Equal(30m, saved.PaidDays);
        Assert.Equal(3000m, saved.BasicPay);
        // OT hours on the bill are approved hours (worked inside an OT assignment); this employee has no assignment
        Assert.Equal(0m, saved.OvertimeHours);
        Assert.Equal(100m, saved.Allowances);
        Assert.Equal(30m, saved.OvertimeAmount);
        Assert.Equal(20m, saved.Deductions);
        // Net Pay = Monthly Salary + Overtime Amount − LOP Deduction = 3100 + 30 − (3100 − 250); allowances / deductions are not in it

        var history = TestHelpers.OkJson(await Controller(context).EmployeeHistory(staff.Id, start.Year, start.Month));
        Assert.Equal(1, history.GetProperty("rows").GetArrayLength());
        Assert.Equal(100m, history.GetProperty("rows")[0].GetProperty("netPay").GetDecimal());
    }

    [Fact]
    public async Task Generate_rejects_staff_ids_not_in_the_selected_pay_period()
    {
        using var context = TestHelpers.CreateContext();
        var start = new DateTime(2026, 10, 1);

        Assert.False(await new IMSB.Backend.Payroll(context).GenerateAsync(start, new[] { new PayBillAdjustmentDto { StaffId = 999999 } }));

        Assert.Empty(context.PayBillEntries);
    }

    // ── Summary cards ─────────────────────────────────────────────

    [Fact]
    public async Task Pay_bill_data_lists_every_category_even_with_nobody_on_the_bill()
    {
        using var context = TestHelpers.CreateContext();
        context.Departments.Add(new Department { Name = "Zero Staff Category" });
        await context.SaveChangesAsync();

        var report = TestHelpers.OkJson(await Controller(context).Data(2026, 10));

        Assert.Empty(report.GetProperty("rows").EnumerateArray());
        Assert.Equal(0m, report.GetProperty("totalNetPay").GetDecimal());
        var departments = report.GetProperty("departments").EnumerateArray().Select(name => name.GetString()!).ToList();
        Assert.Contains("Zero Staff Category", departments);
        Assert.Equal(departments.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase), departments);
        Assert.Equal(departments.Distinct(), departments);
    }

    [Fact]
    public void Summary_cards_always_draw_and_never_swap_to_a_message()
    {
        var js = File.ReadAllText(Path.Combine(
            typeof(PayrollControllerTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!,
            "wwwroot", "js", "payroll.js"));

        Assert.Contains("new Map(allDepartments.map(", js);
        Assert.Contains("$('payBillSummaryBody').innerHTML = card(t('GrandTotal'), overall, true)", js);
    }

    // ── Net Pay = Monthly Salary + Overtime Amount − LOP Deduction ──

    [Theory]
    [InlineData(10000, 0, 10000, 10000)]          // full month, no OT
    [InlineData(10000, 350, 10000, 10350)]        // full month + OT
    [InlineData(10000, 350, 9000, 9350)]          // ₹1,000 LOP
    [InlineData(10000, 0, 0, 0)]                  // nothing paid
    [InlineData(10000, 346.5, 0, 346.5)]          // no paid days, OT only
    [InlineData(31000, 1200, 28000, 29200)]       // 31000 + 1200 − 3000
    public void Net_pay_is_monthly_salary_plus_ot_minus_lop(decimal salary, decimal ot, decimal basicPay, decimal expected)
    {
        Assert.Equal(salary - basicPay, IMSB.Backend.Payroll.LopDeduction(salary, basicPay));
        Assert.Equal(expected, IMSB.Backend.Payroll.NetPay(salary, ot, basicPay));
        Assert.Equal(salary + ot - IMSB.Backend.Payroll.LopDeduction(salary, basicPay), IMSB.Backend.Payroll.NetPay(salary, ot, basicPay));
    }

    [Theory]
    [InlineData(2026, 11, true)]
    [InlineData(2000, 1, true)]
    [InlineData(1999, 12, false)]
    [InlineData(2026, 0, false)]
    [InlineData(2026, 13, false)]
    [InlineData(null, 5, false)]
    [InlineData(2026, null, false)]
    public void Month_start_is_resolved_only_for_a_valid_year_and_month(int? year, int? month, bool valid)
    {
        Assert.Equal(valid, IMSB.Common.CommonHelpers.TryMonthStart(year, month, out var start));
        Assert.Equal(valid ? new DateTime(year!.Value, month!.Value, 1) : default, start);
    }

    [Fact]
    public async Task Backend_rows_are_narrowed_to_the_exact_employee_id()
    {
        using var context = await ContextWithBill();
        var payroll = new IMSB.Backend.Payroll(context);

        var all = await payroll.RowsAsync(new DateTime(2026, 10, 1));
        var one = await payroll.RowsAsync(new DateTime(2026, 10, 1), employeeId: " 1 ");

        Assert.True(all.Count > 1);
        Assert.Equal("1", Assert.Single(one).EmployeeId);
        Assert.Empty(await payroll.RowsAsync(new DateTime(2026, 10, 1), employeeId: "EMP-"));   // exact, not contains
    }

    [Fact]
    public void Salary_statement_with_zero_paid_days_matches_the_screen()
    {
        // 2 and 1: ₹10,000 salary, 0 / 30 paid days, OT ₹100.00 and ₹205.42
        var rows = new[] { (Salary: 10000m, Ot: 100m), (Salary: 10000m, Ot: 205.42m) }
            .Select(r =>
            {
                var basicPay = IMSB.Backend.Payroll.ProratedPay(r.Salary, 0m, 30);
                return (r.Salary, r.Ot, Lop: IMSB.Backend.Payroll.LopDeduction(r.Salary, basicPay),
                    Net: IMSB.Backend.Payroll.NetPay(r.Salary, r.Ot, basicPay));
            }).ToList();

        Assert.Equal((10000m, 100m, 10000m, 100m), rows[0]);       // 10,000 + 100 − 10,000
        Assert.Equal((10000m, 205.42m, 10000m, 205.42m), rows[1]); // 10,000 + 205.42 − 10,000
        // Grand Total: 20,000 + 305.42 − 20,000 = 305.42
        Assert.Equal((20000m, 305.42m, 20000m, 305.42m),
            (rows.Sum(r => r.Salary), rows.Sum(r => r.Ot), rows.Sum(r => r.Lop), rows.Sum(r => r.Net)));
    }

    [Fact]
    public async Task Every_statement_row_has_net_pay_equal_to_salary_plus_ot_minus_lop()
    {
        using var context = await ContextWithBill();
        var anita = context.Staff.Single(employee => employee.EmployeeId == "1");
        context.Departments.Add(new Department { Id = 9006, Name = "Housekeeping" });
        context.Employees.Single(e => e.EmpId == anita.Id).DepartmentId = 9006;   // tbl_Employee.DepartmentID
        context.OtAssignments.Add(new OtAssignment { StaffId = anita.Id, FromDateTime = new DateTime(2026, 10, 1, 16, 0, 0), ToDateTime = new DateTime(2026, 10, 1, 20, 0, 0), OtHours = 4m });
        await context.SaveChangesAsync();

        var rows = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray().ToList();

        Assert.All(rows, row => Assert.Equal(
            row.GetProperty("monthlySalary").GetDecimal() + row.GetProperty("overtimeAmount").GetDecimal() - row.GetProperty("lopDeduction").GetDecimal(),
            row.GetProperty("netPay").GetDecimal()));
        // Anita: 3100 salary + 4 h × ₹50 − 3000 LOP (1 paid day of 31) = 300
        var row = rows.Single(r => r.GetProperty("employeeId").GetString() == "1");
        Assert.Equal((3100m, 200m, 3000m, 300m), (row.GetProperty("monthlySalary").GetDecimal(), row.GetProperty("overtimeAmount").GetDecimal(),
            row.GetProperty("lopDeduction").GetDecimal(), row.GetProperty("netPay").GetDecimal()));
    }

    [Fact]
    public async Task Net_pay_leaves_out_allowances_and_other_deductions()
    {
        using var context = await ContextWithBill();
        var anita = context.Staff.Single(employee => employee.EmployeeId == "1");

        await new IMSB.Backend.Payroll(context).GenerateAsync(new DateTime(2026, 10, 1),
            new[] { new PayBillAdjustmentDto { StaffId = anita.Id, Allowances = 500m, OvertimeAmount = 75m, Deductions = 40m } });

        var saved = context.PayBillEntries.Single(entry => entry.StaffId == anita.Id);
        Assert.Equal(3100m + 75m - (3100m - 100m), saved.NetPay);   // 175: the 500 allowance and 40 deduction are not counted
        var row = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("employeeId").GetString() == "1");
        Assert.Equal(175m, row.GetProperty("netPay").GetDecimal());
    }

    [Fact]
    public async Task Net_pay_grand_total_is_the_sum_of_the_rows_in_every_export()
    {
        using var context = await ContextWithBill();

        var data = TestHelpers.OkJson(await Controller(context).Data(2026, 10));
        var rows = data.GetProperty("rows").EnumerateArray().ToList();
        var expected = rows.Sum(r => r.GetProperty("monthlySalary").GetDecimal() + r.GetProperty("overtimeAmount").GetDecimal() - r.GetProperty("lopDeduction").GetDecimal());

        Assert.Equal(expected, data.GetProperty("totalNetPay").GetDecimal());
        var excel = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null)).FileContents);
        Assert.Equal(expected.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), excel[^1][^1]);
        var pdf = Assert.IsType<PayBillPrintModel>(Assert.IsType<PartialViewResult>(await Controller(context).PdfDocument(2026, 10, null)).Model);
        Assert.Equal(expected, pdf.TotalNetPay);
    }

    // ── Export ────────────────────────────────────────────────────

    /// <summary>1 and 10 share a prefix on purpose: the staff filter must not mix them up.</summary>
    private static async Task<IMSBContext> ContextWithBill()
    {
        var context = TestHelpers.CreateContext();
        var (first, second) = (TestHelpers.ExistingStaff("1", "Anita Sharma", "9000000093"), TestHelpers.ExistingStaff("10", "Rajesh Singh", "9000000094"));
        first.MonthlySalary = 3100m;
        second.MonthlySalary = 6200m;
        context.AddEmployees(first, second);
        var start = new DateTime(2026, 10, 1);
        // 1 day present and the other 30 absent: 1 payable day and 30 LOP days of 31
        foreach (var employee in new[] { first, second })
        {
            context.AttendanceRecords.Add(new AttendanceRecord { StaffId = employee.Id, AttendanceDate = start, Status = AttendanceStatuses.Present });
            for (var day = 1; day < 31; day++)
                context.AttendanceRecords.Add(new AttendanceRecord { StaffId = employee.Id, AttendanceDate = start.AddDays(day), Status = AttendanceStatuses.Absent });
        }
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task Export_downloads_the_month_with_a_grand_total()
    {
        using var context = await ContextWithBill();

        var file = Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null));

        Assert.Equal("pay_bill_202610.xls", file.FileDownloadName);
        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        var rows = ExcelExportTestHelpers.Rows(file.FileContents);
        Assert.Equal(4, rows.Count);
        Assert.Equal(rows[0].Length, rows[3].Length);
        Assert.Contains("Monthly Salary (₹)", rows[0]);
        Assert.Equal("Net Pay", rows[0][^1]);
        Assert.Equal(new[] { "1", "10" }, rows.Skip(1).Take(2).Select(row => row[0]).OrderBy(id => id, StringComparer.Ordinal));
        var anita = rows.Single(row => row[0] == "1");
        Assert.Equal("Anita Sharma", anita[1]);
        Assert.Equal("1", anita[4]);         // payable days
        Assert.Equal("3100.00", anita[5]);
        var lopDays = Array.IndexOf(rows[0], "LOP Days");
        Assert.Equal(lopDays + 1, Array.IndexOf(rows[0], "LOP Deduction"));   // LOP Days beside LOP Deduction
        Assert.Equal("30", anita[lopDays]);
        Assert.Equal("100.00", anita[^1]);
        Assert.Equal("Grand Total", rows[3][0]);
        Assert.Equal("300.00", rows[3][^1]);
    }

    [Fact]
    public async Task Pay_bill_data_shows_the_ot_amount_and_the_salary_lost_to_unpaid_days()
    {
        using var context = await ContextWithBill();

        var rows = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray().ToList();

        // 1 paid day of 31: 3100 → 100 earned, so 3000 is loss of pay; no OT amount is set before the bill is generated
        var anita = rows.Single(row => row.GetProperty("employeeId").GetString() == "1");
        Assert.Equal(3100m, anita.GetProperty("monthlySalary").GetDecimal());
        Assert.Equal(0m, anita.GetProperty("overtimeAmount").GetDecimal());
        Assert.Equal(3000m, anita.GetProperty("lopDeduction").GetDecimal());
        Assert.Equal(100m, anita.GetProperty("netPay").GetDecimal());
    }

    [Fact]
    public async Task Lop_deduction_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 30, MonthlySalary = 3100m, BasicPay = 3000m, OvertimeAmount = 250m, NetPay = 3250m
        });
        await context.SaveChangesAsync();

        var anita = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("employeeId").GetString() == "1");

        Assert.Equal(250m, anita.GetProperty("overtimeAmount").GetDecimal());
        Assert.Equal(100m, anita.GetProperty("lopDeduction").GetDecimal());

        var export = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "1")).FileContents);
        var lopColumn = Array.IndexOf(export[0], "LOP Deduction");
        Assert.True(lopColumn > 0);
        Assert.Equal("100.00", export[1][lopColumn]);
    }

    [Fact]
    public void Lop_deduction_is_never_negative() =>
        Assert.Equal(0m, new IMSB.Interfaces.PayBillRow(999999, "E", "N", "D", "X", 30, 30, 1000m, 1200m, 0, 0, 0, 0, 1200m).LopDeduction);

    [Fact]
    public async Task Export_with_the_staff_filter_has_only_that_employee()
    {
        using var context = await ContextWithBill();

        var file = Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "1"));

        var rows = ExcelExportTestHelpers.Rows(file.FileContents);
        Assert.Equal(new[] { "1" }, rows.Skip(1).SkipLast(1).Select(row => row[0]));
        Assert.Equal("100.00", rows[^1][^1]);
    }

    [Fact]
    public async Task Export_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 2, MonthlySalary = 3100m, BasicPay = 200m, NetPay = 777m
        });
        await context.SaveChangesAsync();

        var file = Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "1"));

        // saved figures: salary 3100, 2 paid days (200), no OT → Net Pay = 3100 + 0 − 2900 = 200, whatever Net Pay was saved
    }

    [Fact]
    public async Task Export_of_a_month_without_staff_has_the_headers_and_a_zero_total()
    {
        using var context = TestHelpers.CreateContext();

        var rows = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null)).FileContents);

        Assert.Equal(2, rows.Count);
        Assert.Equal("0.00", rows[1][^1]);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(2026, null)]
    [InlineData(2026, 13)]
    [InlineData(1999, 10)]
    public async Task Export_refuses_an_invalid_month(int? year, int? month)
    {
        using var context = TestHelpers.CreateContext();
        Assert.IsType<BadRequestResult>(await Controller(context).Export(year, month, null));
    }

    [Fact]
    public async Task Export_headers_follow_the_language()
    {
        using var context = await ContextWithBill();
        using var _ = new TestHelpers.CultureScope("hi-IN");

        var rows = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null)).FileContents);

        Assert.DoesNotContain("Net Pay", rows[0]);
        Assert.DoesNotContain("Grand Total", rows[^1]);
    }

    [Fact]
    public void Generate_is_gone_from_the_admin_screen_and_controller() =>
        Assert.Null(typeof(PayrollController).GetMethod("Generate"));

    [Fact]
    public async Task Pay_bill_data_includes_inactive_staff_with_attendance_in_the_selected_month()
    {
        using var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("9202", "Former Staff", "9000000092", status: "INACTIVE");
        context.AddEmployees(staff);
        var start = new DateTime(2026, 10, 1);
        context.AttendanceRecords.Add(new AttendanceRecord
        {
            StaffId = staff.Id,
            AttendanceDate = start,
            Status = AttendanceStatuses.Present
        });
        await context.SaveChangesAsync();

        var report = TestHelpers.OkJson(await Controller(context).Data(start.Year, start.Month));

        var row = Assert.Single(report.GetProperty("rows").EnumerateArray());
        Assert.Equal(staff.EmployeeId, row.GetProperty("employeeId").GetString());
    }

    [Theory]
    [InlineData("anita", "9203")]
    [InlineData(" 9204 ", "9204")]
    public async Task Print_filters_pay_bill_rows_by_staff_name_or_employee_id(string filter, string expectedEmployeeId)
    {
        using var context = TestHelpers.CreateContext();
        var first = TestHelpers.ExistingStaff("9203", "Anita Sharma", "9000000093");
        var second = TestHelpers.ExistingStaff("9204", "Rajesh Singh", "9000000094");
        context.AddEmployees(first, second);
        foreach (var employee in new[] { first, second })
            context.PayBillEntries.Add(new PayBillEntry
            {
                StaffId = employee.Id,
                Year = 2026,
                Month = 10,
                EmployeeId = employee.EmployeeId,
                EmployeeName = employee.FullName,
                MonthDays = 31,
                MonthlySalary = employee.MonthlySalary,
                NetPay = 0m
            });
        await context.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await Controller(context).Print(2026, 10, filter));
        var model = Assert.IsType<PayBillPrintModel>(result.Model);

        Assert.Equal(expectedEmployeeId, Assert.Single(model.Rows).EmployeeId);
    }

    // ── PDF export (the document payroll.js downloads as a .pdf) ──

    private static async Task<PayBillPrintModel> PdfModel(IMSBContext context, int? year, int? month, string? employeeId = null)
    {
        var view = Assert.IsType<PartialViewResult>(await Controller(context).PdfDocument(year, month, null, employeeId));
        Assert.Equal("_PayBillDocument", view.ViewName);
        return Assert.IsType<PayBillPrintModel>(view.Model);
    }

    [Fact]
    public async Task Pdf_export_has_the_whole_month_with_its_net_pay_total()
    {
        using var context = await ContextWithBill();

        var model = await PdfModel(context, 2026, 10);

        Assert.Equal(new[] { "1", "10" }, model.Rows.Select(row => row.EmployeeId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(300m, model.TotalNetPay);
        Assert.Contains("2026", model.Period);
    }

    [Fact]
    public async Task Pdf_export_with_the_staff_filter_has_only_that_employee()
    {
        using var context = await ContextWithBill();

        // 1 must not also pick up 10
        var model = await PdfModel(context, 2026, 10, "1");

        Assert.Equal("1", Assert.Single(model.Rows).EmployeeId);
        Assert.Equal(100m, model.TotalNetPay);
    }

    [Fact]
    public async Task Pdf_and_excel_exports_have_the_same_rows_and_total()
    {
        using var context = await ContextWithBill();

        var model = await PdfModel(context, 2026, 10, "10");
        var excel = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "10")).FileContents);

        Assert.Equal(model.Rows.Select(row => row.EmployeeId), excel.Skip(1).SkipLast(1).Select(row => row[0]));
        Assert.Equal(model.TotalNetPay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), excel[^1][^1]);
    }

    [Fact]
    public async Task Pdf_export_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 30, MonthlySalary = 3100m, BasicPay = 3000m, OvertimeAmount = 250m, NetPay = 3250m
        });
        await context.SaveChangesAsync();

        var row = Assert.Single((await PdfModel(context, 2026, 10, "1")).Rows);

        Assert.Equal((3250m, 250m, 100m), (row.NetPay, row.OvertimeAmount, row.LopDeduction));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(2026, null)]
    [InlineData(2026, 13)]
    [InlineData(1999, 10)]
    public async Task Pdf_export_refuses_an_invalid_month(int? year, int? month)
    {
        using var context = TestHelpers.CreateContext();
        Assert.IsType<BadRequestResult>(await Controller(context).PdfDocument(year, month, null));
        Assert.IsType<BadRequestResult>(await Controller(context).Print(year, month, null));
    }

    [Fact]
    public async Task Pdf_export_of_a_month_without_staff_is_empty_with_a_zero_total()
    {
        using var context = TestHelpers.CreateContext();

        var model = await PdfModel(context, 2026, 10);

        Assert.Empty(model.Rows);
        Assert.Equal(0m, model.TotalNetPay);
    }

    [Fact]
    public async Task Print_page_has_the_same_rows_as_the_pdf()
    {
        using var context = await ContextWithBill();

        var print = Assert.IsType<PayBillPrintModel>(
            Assert.IsType<ViewResult>(await Controller(context).Print(2026, 10, null, "1")).Model);

        Assert.Equal((await PdfModel(context, 2026, 10, "1")).Rows, print.Rows);
    }

    private static PayrollController Controller(IMSBContext context) => new(new IMSB.Backend.Payroll(context), TestHelpers.LocalizerFactory())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }, "Test"))
            }
        }
    };
}
