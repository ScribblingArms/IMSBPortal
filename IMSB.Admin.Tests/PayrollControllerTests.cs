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
        Assert.Equal(2.5m, saved.OvertimeHours);
        Assert.Equal(100m, saved.Allowances);
        Assert.Equal(30m, saved.OvertimeAmount);
        Assert.Equal(20m, saved.Deductions);
        Assert.Equal(360m, saved.NetPay);

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

        Assert.Equal("777.00", ExcelExportTestHelpers.Rows(file.FileContents).Single(row => row[0] == "EMP-1")[^1]);
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
