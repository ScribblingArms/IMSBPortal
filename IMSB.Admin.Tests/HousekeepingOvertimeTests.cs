using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>
/// Housekeeping OT: OT Amount = total approved OT hours × ₹50, shown on the Monthly Reports, the Salary Report and the
/// Pay Bill (screens and Excel exports) with the same figures.
/// </summary>
public class HousekeepingOvertimeTests
{
    private static readonly DateTime October = new(2026, 10, 1);
    private static readonly Guid HousekeepingDept = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // ── The rule ──────────────────────────────────────────────────
    [Theory]
    [InlineData(1, 50)]
    [InlineData(4, 200)]
    [InlineData(2.5, 125)]
    [InlineData(0.25, 12.5)]       // 15 minutes
    [InlineData(10.75, 537.5)]
    [InlineData(0, 0)]
    [InlineData(-2, 0)]
    public void Housekeeping_ot_amount_is_approved_hours_times_50(decimal hours, decimal expected)
    {
        Assert.Equal(expected, OvertimePay.HousekeepingAmount(hours));
        Assert.Equal(expected, OvertimePay.Amount(hours, "Housekeeping", 12000m, 8m));
    }

    [Fact]
    public void The_housekeeping_rate_is_50_rupees_an_hour() => Assert.Equal(50m, OvertimePay.HousekeepingHourlyRate);

    [Theory]
    [InlineData("Housekeeping", true)]
    [InlineData("HOUSEKEEPING", true)]
    [InlineData("House Keeping", true)]
    [InlineData("Housekeeping Staff", true)]
    [InlineData("Security", false)]
    [InlineData("Sales", false)]
    [InlineData(null, false)]
    public void Housekeeping_category_is_recognised_by_name(string? category, bool expected) =>
        Assert.Equal(expected, OvertimePay.IsHousekeeping(category));

    [Theory]
    [InlineData(8000)]
    [InlineData(12000)]
    [InlineData(25000)]
    public void Housekeeping_ot_does_not_depend_on_salary_or_shift(decimal salary)
    {
        Assert.Equal(200m, OvertimePay.Amount(4m, "Housekeeping", salary, 8m));
        Assert.Equal(200m, OvertimePay.Amount(4m, "Housekeeping", salary, 7m));
    }

    // ── The three screens ─────────────────────────────────────────
    /// <summary>
    /// A housekeeper (₹12,000, 8AM-4PM) with OT assignments 16:00-19:00 on 5 Oct and 16:00-20:00 on 6 Oct, saved as
    /// 3 + 4 = 7 OT hours = ₹350. Also late on 7 Oct without an assignment (not paid).
    /// </summary>
    private static (IMSBContext Context, Staff Housekeeper) Seed()
    {
        var context = TestHelpers.CreateContext();
        context.Departments.Add(new Department { Id = HousekeepingDept, Name = "Housekeeping" });
        var housekeeper = TestHelpers.ExistingStaff("EMP-HK1", "Lakshmi Devi", "9000000301", dept: HousekeepingDept);
        housekeeper.Designation = "Housekeeping Staff";
        housekeeper.MonthlySalary = 12000m;
        context.Staff.Add(housekeeper);

        AttendanceRecord Day(int day, double outHour) => new()
        {
            StaffId = housekeeper.Id, AttendanceDate = October.AddDays(day - 1), Status = AttendanceStatuses.Present,
            InTime = TimeSpan.FromHours(8), OutTime = TimeSpan.FromHours(outHour)
        };
        context.AttendanceRecords.AddRange(Day(5, 19), Day(6, 20), Day(7, 21));
        context.OtAssignments.AddRange(
            new OtAssignment { StaffId = housekeeper.Id, FromDateTime = October.AddDays(4).AddHours(16), ToDateTime = October.AddDays(4).AddHours(19), OtHours = 3m },
            new OtAssignment { StaffId = housekeeper.Id, FromDateTime = October.AddDays(5).AddHours(16), ToDateTime = October.AddDays(5).AddHours(20), OtHours = 4m });
        context.SaveChanges();
        return (context, housekeeper);
    }

    private static T WithAdmin<T>(T controller) where T : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin") }, "Test"))
            }
        };
        return controller;
    }

    private static ReportsController Reports(IMSBContext context) => ApplicationTestHelpers.Reports(context);

    private static PayrollController Payroll(IMSBContext context) =>
        WithAdmin(new PayrollController(new IMSB.Backend.Payroll(context), TestHelpers.LocalizerFactory()));

    // ── Monthly Reports: OT Hrs = approved OT hours ───────────────
    [Fact]
    public async Task Monthly_report_ot_hrs_are_the_approved_hours()
    {
        var (context, _) = Seed();
        using (context)
        {
            var row = TestHelpers.OkJson(await Reports(context).MonthlyAttendance(2026, 10, null)).GetProperty("rows")[0];

            // the 3 h + 4 h saved on the assignments; the late evening of 7 Oct without an assignment does not count
            Assert.Equal(7m, row.GetProperty("otHours").GetDecimal());
            Assert.False(row.TryGetProperty("otAmount", out _));
            Assert.False(row.TryGetProperty("approvedOtHours", out _));
        }
    }

    [Fact]
    public async Task Monthly_report_export_has_the_approved_ot_hrs()
    {
        var (context, _) = Seed();
        using (context)
        {
            var rows = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Reports(context).MonthlyAttendanceExport(2026, 10, null)).FileContents);

            Assert.Equal("OT Hrs", rows[0][^1]);
            Assert.DoesNotContain("Overtime Amount", rows[0]);
            Assert.Equal("7", rows.Single(r => r[1] == "EMP-HK1")[^1]);
        }
    }

    [Fact]
    public async Task Saved_ot_hours_count_even_if_attendance_shows_less()
    {
        var (context, housekeeper) = Seed();
        using (context)
        {
            // left at 17:00 on 6 Oct (1 hour into the 4-hour assignment) and no attendance at all on 5 Oct
            context.AttendanceRecords.RemoveRange(context.AttendanceRecords.Where(r => r.AttendanceDate == October.AddDays(4)));
            context.AttendanceRecords.Single(r => r.AttendanceDate == October.AddDays(5)).OutTime = TimeSpan.FromHours(17);
            await context.SaveChangesAsync();

            var monthly = TestHelpers.OkJson(await Reports(context).MonthlyAttendance(2026, 10, null)).GetProperty("rows")[0];
            var payBill = TestHelpers.OkJson(await Payroll(context).Data(2026, 10)).GetProperty("rows")[0];

            Assert.Equal(7m, monthly.GetProperty("otHours").GetDecimal());
            Assert.Equal(350m, payBill.GetProperty("overtimeAmount").GetDecimal());
        }
    }

    [Fact]
    public async Task Editing_an_assignment_changes_the_ot_hrs_on_every_screen()
    {
        var (context, _) = Seed();
        using (context)
        {
            var assignment = context.OtAssignments.Single(a => a.OtHours == 4m);
            assignment.ToDateTime = assignment.FromDateTime.AddHours(6);
            assignment.OtHours = OtAssignment.HoursBetween(assignment.FromDateTime, assignment.ToDateTime);
            await context.SaveChangesAsync();

            var monthly = TestHelpers.OkJson(await Reports(context).MonthlyAttendance(2026, 10, null)).GetProperty("rows")[0];
            var salary = TestHelpers.OkJson(await Reports(context).Salary(2026, 10, null, null)).GetProperty("rows")[0];
            var payBill = TestHelpers.OkJson(await Payroll(context).Data(2026, 10)).GetProperty("rows")[0];

            Assert.Equal((9m, 9m, 450m), (monthly.GetProperty("otHours").GetDecimal(), salary.GetProperty("otHours").GetDecimal(),
                payBill.GetProperty("overtimeAmount").GetDecimal()));
        }
    }

    // ── Salary Report: OT Hrs column; Total Salary includes the OT amount ──
    [Fact]
    public async Task Salary_report_shows_ot_hrs_and_adds_the_ot_amount_to_total_salary()
    {
        var (context, _) = Seed();
        using (context)
        {
            var report = TestHelpers.OkJson(await Reports(context).Salary(2026, 10, null, null));
            var row = report.GetProperty("rows")[0];

            Assert.Equal(7m, row.GetProperty("otHours").GetDecimal());
            var earned = Math.Round(12000m * 3 / 31, 2, MidpointRounding.AwayFromZero);   // 3 worked days of 31
            Assert.Equal(earned + 7 * 50m, row.GetProperty("totalSalary").GetDecimal());
            Assert.Equal(earned + 350m, report.GetProperty("grandTotalSalary").GetDecimal());
        }
    }

    [Fact]
    public async Task Salary_report_export_has_ot_hrs_and_the_total_with_ot()
    {
        var (context, _) = Seed();
        using (context)
        {
            var rows = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Reports(context).SalaryExport(2026, 10, "Housekeeping", null)).FileContents);

            Assert.Equal(new[] { "Worked Days", "OT Hrs", "Total Salary" }, rows[0][^3..]);
            Assert.DoesNotContain("Overtime Amount", rows[0]);
            Assert.DoesNotContain("Earned Salary", rows[0]);
            var total = (Math.Round(12000m * 3 / 31, 2, MidpointRounding.AwayFromZero) + 350m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var housekeeper = rows.Single(r => r[1] == "EMP-HK1");
            Assert.Equal(("3", "7", total), (housekeeper[6], housekeeper[7], housekeeper[8]));
            Assert.Equal(("Grand Total", "7", total), (rows[^1][0], rows[^1][7], rows[^1][8]));
        }
    }

    // ── Employee-wise Salary Statement (Pay Bill): Overtime Amount ──
    [Fact]
    public async Task Pay_bill_shows_the_housekeeping_ot_and_adds_it_to_net_pay()
    {
        var (context, _) = Seed();
        using (context)
        {
            var row = TestHelpers.OkJson(await Payroll(context).Data(2026, 10)).GetProperty("rows")[0];

            Assert.Equal(7m, row.GetProperty("overtimeHours").GetDecimal());
            Assert.Equal(350m, row.GetProperty("overtimeAmount").GetDecimal());
            Assert.Equal(row.GetProperty("basicPay").GetDecimal() + 350m, row.GetProperty("netPay").GetDecimal());
        }
    }

    [Fact]
    public async Task Pay_bill_exports_have_the_housekeeping_ot()
    {
        var (context, _) = Seed();
        using (context)
        {
            var excel = ExcelExportTestHelpers.Rows(Assert.IsType<FileContentResult>(await Payroll(context).Export(2026, 10, null, "EMP-HK1")).FileContents);
            var otColumn = Array.IndexOf(excel[0], "Overtime Amount");
            Assert.Equal("350.00", excel[1][otColumn]);

            var pdf = Assert.IsType<PayrollController.PayBillPrintModel>(
                Assert.IsType<PartialViewResult>(await Payroll(context).PdfDocument(2026, 10, null, "EMP-HK1")).Model);
            Assert.Equal(350m, Assert.Single(pdf.Rows).OvertimeAmount);
        }
    }

    [Fact]
    public async Task All_three_screens_use_the_same_approved_hours()
    {
        var (context, _) = Seed();
        using (context)
        {
            var monthly = TestHelpers.OkJson(await Reports(context).MonthlyAttendance(2026, 10, null)).GetProperty("rows")[0];
            var salary = TestHelpers.OkJson(await Reports(context).Salary(2026, 10, null, null)).GetProperty("rows")[0];
            var payBill = TestHelpers.OkJson(await Payroll(context).Data(2026, 10)).GetProperty("rows")[0];

            // the same 7 approved hours everywhere, and the Pay Bill pays them at ₹50
            Assert.Equal(new[] { 7m, 7m, 7m }, new[] { monthly.GetProperty("otHours").GetDecimal(),
                salary.GetProperty("otHours").GetDecimal(), payBill.GetProperty("overtimeHours").GetDecimal() });
            Assert.Equal(7m * OvertimePay.HousekeepingHourlyRate, payBill.GetProperty("overtimeAmount").GetDecimal());
        }
    }

    [Fact]
    public async Task Housekeeping_ot_without_an_assignment_is_not_paid_anywhere()
    {
        var (context, housekeeper) = Seed();
        using (context)
        {
            context.OtAssignments.RemoveRange(context.OtAssignments);
            await context.SaveChangesAsync();

            var monthly = TestHelpers.OkJson(await Reports(context).MonthlyAttendance(2026, 10, null)).GetProperty("rows")[0];
            var salary = TestHelpers.OkJson(await Reports(context).Salary(2026, 10, null, null)).GetProperty("rows")[0];
            var payBill = TestHelpers.OkJson(await Payroll(context).Data(2026, 10)).GetProperty("rows")[0];

            Assert.Equal(0m, monthly.GetProperty("otHours").GetDecimal());
            Assert.Equal(0m, salary.GetProperty("otHours").GetDecimal());
            Assert.Equal(salary.GetProperty("earnedSalary").GetDecimal(), salary.GetProperty("totalSalary").GetDecimal());
            Assert.Equal(0m, payBill.GetProperty("overtimeAmount").GetDecimal());
            Assert.Equal(payBill.GetProperty("basicPay").GetDecimal(), payBill.GetProperty("netPay").GetDecimal());
        }
    }
}
