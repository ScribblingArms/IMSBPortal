using IMSB.Backend;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

/// <summary>
/// OT pay by staff category: Security = approved OT hours × (monthly salary ÷ 30), everyone else ₹50 an hour;
/// approved hours = the OT Hrs saved on the OT assignments. Shown on the Pay Bill and the monthly Salary Report.
/// </summary>
public class OvertimePayTests
{
    private static readonly DateTime October = new(2026, 10, 1);
    private static readonly int SecurityDept = 9003;
    private static readonly int HousekeepingDept = 9004;

    // ── The rates ─────────────────────────────────────────────────
    [Theory]
    [InlineData("Housekeeping", 10000, 3, 150)]             // 3 h × ₹50
    [InlineData("Housekeeping", 10000, 2.5, 125)]
    [InlineData("Admin", 40000, 2, 100)]                    // every other category is paid like housekeeping
    [InlineData("Security", 16500, 1, 550)]                 // supervisor: ₹16,500 ÷ 30 = ₹550, × 1 h
    [InlineData("Security", 16500, 4, 2200)]                // 4 h × ₹550
    [InlineData("Security Supervisor", 16500, 8, 4400)]     // 8 h × ₹550
    [InlineData("Security", 15000, 3, 1500)]                // guard: ₹15,000 ÷ 30 = ₹500, × 3 h
    [InlineData("security guards", 15000, 2, 1000)]         // any category name containing "Security", any case
    [InlineData("Security", 16500, 0.5, 275)]
    [InlineData("Security", 10000, 1, 333.33)]              // ₹333.333… rounded to the paisa
    [InlineData("Security", 16500, 0, 0)]
    [InlineData("Housekeeping", 10000, 0, 0)]
    public void Amount_follows_the_staff_category(string category, decimal salary, decimal hours, decimal expected) =>
        Assert.Equal(expected, OvertimePay.Amount(hours, category, salary));

    [Fact]
    public void Security_rate_is_monthly_salary_divided_by_30() =>
        Assert.Equal(550m, OvertimePay.SecurityRate(16500m));

    // ── Saved OT hours ────────────────────────────────────────────
    private static OtAssignment Saved(DateTime day, double fromHour, double toHour) =>
        new() { FromDateTime = day.AddHours(fromHour), ToDateTime = day.AddHours(toHour), OtHours = OtAssignment.HoursBetween(day.AddHours(fromHour), day.AddHours(toHour)) };

    [Theory]
    [InlineData(16, 20, 4)]
    [InlineData(14, 16.5, 2.5)]
    [InlineData(18, 18.25, 0.25)]        // 15 minutes
    [InlineData(22, 26, 4)]              // past midnight
    [InlineData(18, 18, 0)]
    [InlineData(20, 18, 0)]
    public void Assignment_hours_are_to_minus_from(double fromHour, double toHour, decimal expected)
    {
        var day = October.AddDays(4);
        Assert.Equal(expected, OtAssignment.HoursBetween(day.AddHours(fromHour), day.AddHours(toHour)));
    }

    [Fact]
    public void Month_hours_total_the_saved_hours_of_the_months_ot_dates()
    {
        var assignments = new[]
        {
            Saved(October.AddDays(1), 16, 20),          // 4
            Saved(October.AddDays(2), 16, 19.5),        // 3.5
            Saved(October.AddDays(30), 22, 26),         // 31 Oct 22:00 → 1 Nov 02:00: an October OT date, 4
            Saved(October.AddDays(-1), 16, 20),         // 30 Sep: another month
            Saved(October.AddMonths(1), 16, 20)         // 1 Nov: another month
        };

        Assert.Equal(11.5m, OvertimePay.MonthHours(assignments, October));
    }

    [Fact]
    public void Month_hours_use_the_saved_figure_not_the_times()
    {
        var day = October.AddDays(4);
        var edited = new OtAssignment { FromDateTime = day.AddHours(16), ToDateTime = day.AddHours(20), OtHours = 3m };
        Assert.Equal(3m, OvertimePay.MonthHours(new[] { edited }, October));
    }

    [Fact]
    public void No_assignments_means_no_ot_hours() =>
        Assert.Equal(0m, OvertimePay.MonthHours(Array.Empty<OtAssignment>(), October));

    // ── Pay Bill and Salary Report ───────────────────────────────
    /// <summary>
    /// A housekeeper and a security supervisor on the 8AM-4PM shift, each with a 16:00-20:00 OT assignment on
    /// 5 Oct (4 saved OT hours), and a sales person who worked late without an assignment.
    /// </summary>
    private static (IMSBContext Context, Staff Housekeeper, Staff Supervisor, Staff Sales) Seed()
    {
        var context = TestHelpers.CreateContext();
        context.Departments.AddRange(new Department { Id = SecurityDept, Name = "Security" }, new Department { Id = HousekeepingDept, Name = "Housekeeping" });
        var housekeeper = TestHelpers.ExistingStaff("EMP-HK", "House Keeper", "9000000201", dept: HousekeepingDept);
        housekeeper.MonthlySalary = 10000m;
        var supervisor = TestHelpers.ExistingStaff("EMP-SS", "Security Supervisor", "9000000202", dept: SecurityDept);
        supervisor.MonthlySalary = 16500m;
        var sales = TestHelpers.ExistingStaff("EMP-SL", "Sales Person", "9000000203", dept: TestHelpers.SalesDept);
        sales.MonthlySalary = 20000m;
        context.Staff.AddRange(housekeeper, supervisor, sales);

        var day = October.AddDays(4);
        foreach (var employee in new[] { housekeeper, supervisor, sales })
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                StaffId = employee.Id, AttendanceDate = day, Status = AttendanceStatuses.Present,
                InTime = TimeSpan.FromHours(8), OutTime = TimeSpan.FromHours(20)
            });
        foreach (var employee in new[] { housekeeper, supervisor })
            context.OtAssignments.Add(new OtAssignment { StaffId = employee.Id, FromDateTime = day.AddHours(16), ToDateTime = day.AddHours(20), OtHours = 4m });
        context.SaveChanges();
        return (context, housekeeper, supervisor, sales);
    }

    [Fact]
    public async Task Pay_bill_shows_the_ot_amount_and_adds_it_to_net_pay()
    {
        var (context, housekeeper, supervisor, sales) = Seed();
        using (context)
        {
            var rows = (await new Payroll(context).RowsAsync(October)).ToDictionary(row => row.StaffId);

            var hk = rows[housekeeper.Id];
            Assert.Equal((4m, 200m), (hk.OvertimeHours, hk.OvertimeAmount));               // 4 h × ₹50
            Assert.Equal(hk.BasicPay + 200m, hk.NetPay);

            var ss = rows[supervisor.Id];
            Assert.Equal((4m, 2200m), (ss.OvertimeHours, ss.OvertimeAmount));              // 4 h × ₹550 (₹16,500 ÷ 30)
            Assert.Equal(ss.BasicPay + 2200m, ss.NetPay);

            var sl = rows[sales.Id];
            Assert.Equal((0m, 0m), (sl.OvertimeHours, sl.OvertimeAmount));                 // late, but no OT assignment
            Assert.Equal(sl.BasicPay, sl.NetPay);
        }
    }

    [Fact]
    public async Task Generating_the_pay_bill_saves_the_calculated_ot()
    {
        var (context, housekeeper, supervisor, _) = Seed();
        using (context)
        {
            Assert.True(await new Payroll(context).GenerateAsync(October, Array.Empty<PayBillAdjustmentDto>()));

            var saved = context.PayBillEntries.ToDictionary(entry => entry.StaffId);
            Assert.Equal((4m, 200m), (saved[housekeeper.Id].OvertimeHours, saved[housekeeper.Id].OvertimeAmount));
            Assert.Equal((4m, 2200m), (saved[supervisor.Id].OvertimeHours, saved[supervisor.Id].OvertimeAmount));
            Assert.Equal(saved[supervisor.Id].BasicPay + 2200m, saved[supervisor.Id].NetPay);
        }
    }

    [Fact]
    public async Task A_given_ot_amount_still_overrides_the_calculation_when_generating()
    {
        var (context, housekeeper, _, _) = Seed();
        using (context)
        {
            await new Payroll(context).GenerateAsync(October, new[] { new PayBillAdjustmentDto { StaffId = housekeeper.Id, OvertimeAmount = 999m } });

            Assert.Equal(999m, context.PayBillEntries.Single(entry => entry.StaffId == housekeeper.Id).OvertimeAmount);
        }
    }

    [Fact]
    public async Task A_generated_month_keeps_its_saved_ot()
    {
        var (context, housekeeper, _, _) = Seed();
        using (context)
        {
            context.PayBillEntries.Add(new PayBillEntry
            {
                StaffId = housekeeper.Id, Year = 2026, Month = 10, EmployeeId = housekeeper.EmployeeId, EmployeeName = housekeeper.FullName,
                MonthDays = 31, PaidDays = 1, MonthlySalary = 10000m, BasicPay = 322.58m, OvertimeHours = 1m, OvertimeAmount = 50m, NetPay = 372.58m
            });
            await context.SaveChangesAsync();

            var row = (await new Payroll(context).RowsAsync(October)).Single(r => r.StaffId == housekeeper.Id);

            Assert.Equal((1m, 50m, 372.58m), (row.OvertimeHours, row.OvertimeAmount, row.NetPay));
        }
    }

    [Fact]
    public async Task Salary_report_shows_earned_salary_ot_and_a_total_including_ot()
    {
        var (context, housekeeper, supervisor, sales) = Seed();
        using (context)
        {
            var report = await new IMSB.Backend.Attendance(context).SalaryReportAsync(October, null, null);
            var rows = report.Rows.ToDictionary(row => row.EmployeeId);

            var hk = rows[housekeeper.EmployeeId];
            Assert.Equal(Math.Round(10000m / 31, 2, MidpointRounding.AwayFromZero), hk.EarnedSalary);   // 1 worked day of 31
            Assert.Equal((4m, 200m), (hk.OtHours, hk.OtAmount));
            Assert.Equal(hk.EarnedSalary + 200m, hk.TotalSalary);

            var ss = rows[supervisor.EmployeeId];
            Assert.Equal((4m, 2200m), (ss.OtHours, ss.OtAmount));
            Assert.Equal(ss.EarnedSalary + 2200m, ss.TotalSalary);

            Assert.Equal((0m, 0m), (rows[sales.EmployeeId].OtHours, rows[sales.EmployeeId].OtAmount));
            Assert.Equal(report.Rows.Sum(row => row.EarnedSalary) + 2400m, report.GrandTotalSalary);   // 200 + 2200
        }
    }

    [Fact]
    public async Task Pay_bill_and_salary_report_agree_on_ot()
    {
        var (context, _, _, _) = Seed();
        using (context)
        {
            var bill = (await new Payroll(context).RowsAsync(October)).ToDictionary(row => row.EmployeeId, row => (row.OvertimeHours, row.OvertimeAmount));
            var report = (await new IMSB.Backend.Attendance(context).SalaryReportAsync(October, null, null)).Rows
                .ToDictionary(row => row.EmployeeId, row => (row.OtHours, row.OtAmount));

            Assert.Equal(bill, report);
        }
    }
}
