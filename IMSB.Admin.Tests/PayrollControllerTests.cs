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

        Assert.Equal(2.5m, PayrollController.PaidDays(records));
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

        var result = await Controller(context).Generate(new GeneratePayBillDto
        {
            Year = start.Year,
            Month = start.Month,
            Rows = new() { new PayBillAdjustmentDto { StaffId = staff.Id, Allowances = 100m, OvertimeAmount = 30m, Deductions = 20m } }
        });

        Assert.IsType<OkObjectResult>(result);
        var saved = Assert.Single(context.PayBillEntries.Where(entry => entry.Year == start.Year && entry.Month == start.Month));
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
        var result = await Controller(context).Generate(new GeneratePayBillDto
        {
            Year = start.Year,
            Month = start.Month,
            Rows = new() { new PayBillAdjustmentDto { StaffId = Guid.NewGuid() } }
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.PayBillEntries);
    }

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

    private static PayrollController Controller(IMSBContext context) => new(context, TestHelpers.LocalizerFactory())
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
