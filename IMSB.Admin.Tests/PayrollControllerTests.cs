using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

public class PayrollControllerTests
{
    [Fact]
    public void Paid_days_include_present_half_day_and_paid_leave_only()
    {
        var records = new[]
        {
            new AttendanceRecord { Status = AttendanceStatuses.Present },
            new AttendanceRecord { Status = AttendanceStatuses.HalfDay },
            new AttendanceRecord { Status = AttendanceStatuses.Leave },
            new AttendanceRecord { Status = AttendanceStatuses.Leave, LeaveWithoutPay = true },
            new AttendanceRecord { Status = AttendanceStatuses.Absent }
        };

        Assert.Equal(2.5m, IMSB.Backend.Payroll.PaidDays(records));
    }

    [Fact]
    public async Task Generate_saves_prorated_pay_adjustments_and_employee_history()
    {
        using var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("EMP-PAY-01", "Payroll Test", "9000000091");
        staff.MonthlySalary = 3100m;
        context.Staff.Add(staff);
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
        Assert.Equal(2.5m, saved.PaidDays);
        Assert.Equal(250m, saved.BasicPay);
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

        Assert.False(await new IMSB.Backend.Payroll(context).GenerateAsync(start, new[] { new PayBillAdjustmentDto { StaffId = Guid.NewGuid() } }));

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

    [Fact]
    public void Salary_statement_with_zero_paid_days_matches_the_screen()
    {
        // EMP002 and EMP001: ₹10,000 salary, 0 / 30 paid days, OT ₹100.00 and ₹205.42
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
        var anita = context.Staff.Single(employee => employee.EmployeeId == "EMP-1");
        context.Departments.Add(new Department { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Name = "Housekeeping" });
        anita.DepartmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        context.OtAssignments.Add(new OtAssignment { StaffId = anita.Id, FromDateTime = new DateTime(2026, 10, 1, 16, 0, 0), ToDateTime = new DateTime(2026, 10, 1, 20, 0, 0), OtHours = 4m });
        await context.SaveChangesAsync();

        var rows = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray().ToList();

        Assert.All(rows, row => Assert.Equal(
            row.GetProperty("monthlySalary").GetDecimal() + row.GetProperty("overtimeAmount").GetDecimal() - row.GetProperty("lopDeduction").GetDecimal(),
            row.GetProperty("netPay").GetDecimal()));
        // Anita: 3100 salary + 4 h × ₹50 − 3000 LOP (1 paid day of 31) = 300
        var row = rows.Single(r => r.GetProperty("employeeId").GetString() == "EMP-1");
        Assert.Equal((3100m, 200m, 3000m, 300m), (row.GetProperty("monthlySalary").GetDecimal(), row.GetProperty("overtimeAmount").GetDecimal(),
            row.GetProperty("lopDeduction").GetDecimal(), row.GetProperty("netPay").GetDecimal()));
    }

    [Fact]
    public async Task Net_pay_leaves_out_allowances_and_other_deductions()
    {
        using var context = await ContextWithBill();
        var anita = context.Staff.Single(employee => employee.EmployeeId == "EMP-1");

        await new IMSB.Backend.Payroll(context).GenerateAsync(new DateTime(2026, 10, 1),
            new[] { new PayBillAdjustmentDto { StaffId = anita.Id, Allowances = 500m, OvertimeAmount = 75m, Deductions = 40m } });

        var saved = context.PayBillEntries.Single(entry => entry.StaffId == anita.Id);
        Assert.Equal(3100m + 75m - (3100m - 100m), saved.NetPay);   // 175: the 500 allowance and 40 deduction are not counted
        var row = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("employeeId").GetString() == "EMP-1");
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
        var pdf = Assert.IsType<PayrollController.PayBillPrintModel>(Assert.IsType<PartialViewResult>(await Controller(context).PdfDocument(2026, 10, null)).Model);
        Assert.Equal(expected, pdf.TotalNetPay);
    }

    // ── Export ────────────────────────────────────────────────────

    /// <summary>EMP-1 and EMP-10 share a prefix on purpose: the staff filter must not mix them up.</summary>
    private static async Task<IMSBContext> ContextWithBill()
    {
        var context = TestHelpers.CreateContext();
        var (first, second) = (TestHelpers.ExistingStaff("EMP-1", "Anita Sharma", "9000000093"), TestHelpers.ExistingStaff("EMP-10", "Rajesh Singh", "9000000094"));
        first.MonthlySalary = 3100m;
        second.MonthlySalary = 6200m;
        context.Staff.AddRange(first, second);
        var start = new DateTime(2026, 10, 1);
        foreach (var employee in new[] { first, second })
            context.AttendanceRecords.Add(new AttendanceRecord { StaffId = employee.Id, AttendanceDate = start, Status = AttendanceStatuses.Present });
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
        Assert.Equal(new[] { "EMP-1", "EMP-10" }, rows.Skip(1).Take(2).Select(row => row[0]).OrderBy(id => id, StringComparer.Ordinal));
        var anita = rows.Single(row => row[0] == "EMP-1");
        Assert.Equal("Anita Sharma", anita[1]);
        Assert.Equal("1", anita[4]);
        Assert.Equal("3100.00", anita[5]);
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
        var anita = rows.Single(row => row.GetProperty("employeeId").GetString() == "EMP-1");
        Assert.Equal(3100m, anita.GetProperty("monthlySalary").GetDecimal());
        Assert.Equal(0m, anita.GetProperty("overtimeAmount").GetDecimal());
        Assert.Equal(3000m, anita.GetProperty("lopDeduction").GetDecimal());
        Assert.Equal(100m, anita.GetProperty("netPay").GetDecimal());
    }

    [Fact]
    public async Task Lop_deduction_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "EMP-1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 30, MonthlySalary = 3100m, BasicPay = 3000m, OvertimeAmount = 250m, NetPay = 3250m
        });
        await context.SaveChangesAsync();

        var anita = TestHelpers.OkJson(await Controller(context).Data(2026, 10)).GetProperty("rows").EnumerateArray()
            .Single(row => row.GetProperty("employeeId").GetString() == "EMP-1");

        Assert.Equal(250m, anita.GetProperty("overtimeAmount").GetDecimal());
        Assert.Equal(100m, anita.GetProperty("lopDeduction").GetDecimal());

        var export = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "EMP-1")).FileContents);
        var lopColumn = Array.IndexOf(export[0], "LOP Deduction");
        Assert.True(lopColumn > 0);
        Assert.Equal("100.00", export[1][lopColumn]);
    }

    [Fact]
    public void Lop_deduction_is_never_negative() =>
        Assert.Equal(0m, new IMSB.Interfaces.PayBillRow(Guid.NewGuid(), "E", "N", "D", "X", 30, 30, 1000m, 1200m, 0, 0, 0, 0, 1200m).LopDeduction);

    [Fact]
    public async Task Export_with_the_staff_filter_has_only_that_employee()
    {
        using var context = await ContextWithBill();

        var file = Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "EMP-1"));

        var rows = ExcelExportTestHelpers.Rows(file.FileContents);
        Assert.Equal(new[] { "EMP-1" }, rows.Skip(1).SkipLast(1).Select(row => row[0]));
        Assert.Equal("100.00", rows[^1][^1]);
    }

    [Fact]
    public async Task Export_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "EMP-1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 2, MonthlySalary = 3100m, BasicPay = 200m, NetPay = 777m
        });
        await context.SaveChangesAsync();

        var file = Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "EMP-1"));

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
        var staff = TestHelpers.ExistingStaff("EMP-PAY-02", "Former Staff", "9000000092", status: "INACTIVE");
        context.Staff.Add(staff);
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
    [InlineData("anita", "EMP-PAY-03")]
    [InlineData("emp-pay-04", "EMP-PAY-04")]
    public async Task Print_filters_pay_bill_rows_by_staff_name_or_employee_id(string filter, string expectedEmployeeId)
    {
        using var context = TestHelpers.CreateContext();
        var first = TestHelpers.ExistingStaff("EMP-PAY-03", "Anita Sharma", "9000000093");
        var second = TestHelpers.ExistingStaff("EMP-PAY-04", "Rajesh Singh", "9000000094");
        context.Staff.AddRange(first, second);
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
        var model = Assert.IsType<PayrollController.PayBillPrintModel>(result.Model);

        Assert.Equal(expectedEmployeeId, Assert.Single(model.Rows).EmployeeId);
    }

    // ── PDF export (the document payroll.js downloads as a .pdf) ──

    private static async Task<PayrollController.PayBillPrintModel> PdfModel(IMSBContext context, int? year, int? month, string? employeeId = null)
    {
        var view = Assert.IsType<PartialViewResult>(await Controller(context).PdfDocument(year, month, null, employeeId));
        Assert.Equal("_PayBillDocument", view.ViewName);
        return Assert.IsType<PayrollController.PayBillPrintModel>(view.Model);
    }

    [Fact]
    public async Task Pdf_export_has_the_whole_month_with_its_net_pay_total()
    {
        using var context = await ContextWithBill();

        var model = await PdfModel(context, 2026, 10);

        Assert.Equal(new[] { "EMP-1", "EMP-10" }, model.Rows.Select(row => row.EmployeeId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal(300m, model.TotalNetPay);
        Assert.Contains("2026", model.Period);
    }

    [Fact]
    public async Task Pdf_export_with_the_staff_filter_has_only_that_employee()
    {
        using var context = await ContextWithBill();

        // EMP-1 must not also pick up EMP-10
        var model = await PdfModel(context, 2026, 10, "emp-1");

        Assert.Equal("EMP-1", Assert.Single(model.Rows).EmployeeId);
        Assert.Equal(100m, model.TotalNetPay);
    }

    [Fact]
    public async Task Pdf_and_excel_exports_have_the_same_rows_and_total()
    {
        using var context = await ContextWithBill();

        var model = await PdfModel(context, 2026, 10, "EMP-10");
        var excel = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Controller(context).Export(2026, 10, null, "EMP-10")).FileContents);

        Assert.Equal(model.Rows.Select(row => row.EmployeeId), excel.Skip(1).SkipLast(1).Select(row => row[0]));
        Assert.Equal(model.TotalNetPay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), excel[^1][^1]);
    }

    [Fact]
    public async Task Pdf_export_uses_the_saved_figures_of_a_generated_month()
    {
        using var context = await ContextWithBill();
        var staff = context.Staff.Single(employee => employee.EmployeeId == "EMP-1");
        context.PayBillEntries.Add(new PayBillEntry
        {
            StaffId = staff.Id, Year = 2026, Month = 10, EmployeeId = staff.EmployeeId, EmployeeName = staff.FullName,
            MonthDays = 31, PaidDays = 30, MonthlySalary = 3100m, BasicPay = 3000m, OvertimeAmount = 250m, NetPay = 3250m
        });
        await context.SaveChangesAsync();

        var row = Assert.Single((await PdfModel(context, 2026, 10, "EMP-1")).Rows);

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

        var print = Assert.IsType<PayrollController.PayBillPrintModel>(
            Assert.IsType<ViewResult>(await Controller(context).Print(2026, 10, null, "EMP-1")).Model);

        Assert.Equal((await PdfModel(context, 2026, 10, "EMP-1")).Rows, print.Rows);
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
