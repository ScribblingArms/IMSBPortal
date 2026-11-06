using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

public class LeavesControllerTests
{
    private static readonly int Year = DateTime.Today.Year;
    private static readonly DateTime LeaveDay = DateTime.Today.AddDays(2);
    private static readonly DateTime BackDay = new(DateTime.Today.Year, 1, 1);

    private static (IMSBContext Context, LeavesController Controller, Staff Staff) Create()
    {
        var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("EMP901", "Leave Tester", "900000901");
        staff.StaffCategory = "Operators";
        context.Staff.Add(staff);
        context.SaveChanges();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "hr@imsb"),
                new Claim(ClaimTypes.Role, "Staff")
            }, "Test"))
        };
        var controller = new LeavesController(new IMSB.Backend.Leaves(context), TestHelpers.LocalizerFactory())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TestTempDataProvider())
        };
        return (context, controller, staff);
    }

    private static LeaveEntitlementForm Entitlement(Guid staffId, decimal quota = 5m) => new()
    {
        StaffId = staffId,
        LeaveType = "Casual",
        AnnualQuota = quota
    };

    private static LeaveEntryForm Entry(Guid staffId, DateTime? day = null, decimal days = 1m) => new()
    {
        StaffId = staffId,
        LeaveType = "Casual",
        FromDate = (day ?? LeaveDay).ToString("yyyy-MM-dd"),
        ToDate = (day ?? LeaveDay).ToString("yyyy-MM-dd"),
        Days = days,
        Remarks = "personal"
    };

    private static void Bind(LeavesController controller, object model) => TestHelpers.BindModelState(controller, model);

    [Fact]
    public async Task Index_shows_employee_entitlements_and_current_year_balances()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 5m
            });
            context.StaffLeaveEntries.Add(new StaffLeaveEntry
            {
                StaffId = staff.Id, LeaveType = "Casual", Year = Year, FromDate = LeaveDay,
                ToDate = LeaveDay, Days = 1m
            });
            context.StaffLeaveEntries.Add(new StaffLeaveEntry
            {
                StaffId = staff.Id, LeaveType = "Casual", Year = Year - 1, FromDate = LeaveDay.AddYears(-1),
                ToDate = LeaveDay.AddYears(-1), Days = 2m
            });
            await context.SaveChangesAsync();

            var result = Assert.IsType<ViewResult>(await controller.Index());
            var model = Assert.IsType<LeaveManagementViewModel>(result.Model);
            var balance = Assert.Single(model.Balances);
            Assert.Equal(Year, model.Year);
            Assert.Equal("Sales", balance.StaffCategory);
            Assert.Equal(1m, balance.Taken);
            Assert.Equal(4m, balance.Balance);
        }
    }

    [Fact]
    public async Task Save_entitlement_creates_and_updates_current_year_employee_type()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            var form = Entitlement(staff.Id);
            Bind(controller, form);
            Assert.IsType<RedirectToActionResult>(await controller.SaveEntitlement(form));
            Assert.Equal(5m, (await context.LeaveEntitlements.SingleAsync()).AnnualQuota);

            controller.ModelState.Clear();
            var update = Entitlement(staff.Id, 8m);
            Bind(controller, update);
            Assert.IsType<RedirectToActionResult>(await controller.SaveEntitlement(update));
            Assert.Equal(1, await context.LeaveEntitlements.CountAsync());
            Assert.Equal(8m, (await context.LeaveEntitlements.SingleAsync()).AnnualQuota);
        }
    }

    [Fact]
    public async Task Quota_cannot_be_reduced_below_days_already_taken()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 4m
            });
            context.StaffLeaveEntries.Add(new StaffLeaveEntry
            {
                StaffId = staff.Id, LeaveType = "Casual", Year = Year, FromDate = LeaveDay,
                ToDate = LeaveDay, Days = 2m
            });
            await context.SaveChangesAsync();
            var form = Entitlement(staff.Id, 1m);
            Bind(controller, form);

            Assert.IsType<RedirectToActionResult>(await controller.SaveEntitlement(form));
            Assert.Equal(4m, (await context.LeaveEntitlements.SingleAsync()).AnnualQuota);
        }
    }

    [Fact]
    public async Task Selecting_attendance_leave_type_assigns_uncovered_attendance_as_lop_and_updates_quota_dynamically()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.AttendanceRecords.Add(new AttendanceRecord
            {
                StaffId = staff.Id,
                AttendanceDate = DateTime.Today.AddDays(-1),
                Status = AttendanceStatuses.Leave
            });
            await context.SaveChangesAsync();
            var form = Entitlement(staff.Id, 0.5m);
            Bind(controller, form);

            Assert.IsType<RedirectToActionResult>(await controller.SaveEntitlement(form));
            var attendance = await context.AttendanceRecords.SingleAsync();
            Assert.True(attendance.LeaveWithoutPay);

            var balance = Assert.Single(((LeaveManagementViewModel)Assert.IsType<ViewResult>(
                await controller.Index()).Model!).Balances);
            Assert.Equal(0m, balance.Taken);
            Assert.Equal(0.5m, balance.Balance);
        }
    }

    [Fact]
    public async Task Record_updates_balance_and_entry_can_be_corrected_or_deleted()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 5m
            });
            await context.SaveChangesAsync();
            var form = Entry(staff.Id);
            Bind(controller, form);

            Assert.IsType<RedirectToActionResult>(await controller.Record(form));
            var recorded = await context.StaffLeaveEntries.SingleAsync();
            Assert.Equal("hr@imsb", recorded.CreatedBy);
            Assert.Equal(4m, Assert.Single(((LeaveManagementViewModel)Assert.IsType<ViewResult>(await controller.Index()).Model!).Balances).Balance);

            var lastYear = new DateTime(Year - 1, 6, 1).ToString("yyyy-MM-dd");
            Assert.IsType<RedirectToActionResult>(await controller.UpdateEntry(recorded.Id, lastYear, lastYear, "last year"));
            Assert.Equal(LeaveDay, (await context.StaffLeaveEntries.SingleAsync()).FromDate);

            controller.ModelState.Clear();
            var scheduledLeave = LeaveDay.AddDays(2);
            Assert.IsType<RedirectToActionResult>(await controller.UpdateEntry(recorded.Id, scheduledLeave.ToString("yyyy-MM-dd"),
                scheduledLeave.AddDays(1).ToString("yyyy-MM-dd"), "two days"));
            Assert.Equal(2m, (await context.StaffLeaveEntries.SingleAsync()).Days);
            Assert.Equal(scheduledLeave, (await context.StaffLeaveEntries.SingleAsync()).FromDate);
            Assert.Equal(3m, Assert.Single(((LeaveManagementViewModel)Assert.IsType<ViewResult>(await controller.Index()).Model!).Balances).Balance);

            Assert.IsType<RedirectToActionResult>(await controller.DeleteEntry(recorded.Id));
            Assert.Empty(context.StaffLeaveEntries);
            Assert.Equal(5m, Assert.Single(((LeaveManagementViewModel)Assert.IsType<ViewResult>(await controller.Index()).Model!).Balances).Balance);
        }
    }

    [Fact]
    public async Task Record_rejects_unconfigured_type_overlapping_dates_and_exceeded_balance()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 1m
            });
            await context.SaveChangesAsync();

            var wrongType = Entry(staff.Id);
            wrongType.LeaveType = "Sick";
            Bind(controller, wrongType);
            await controller.Record(wrongType);
            Assert.Empty(context.StaffLeaveEntries);

            controller.ModelState.Clear();
            var first = Entry(staff.Id);
            Bind(controller, first);
            await controller.Record(first);
            Assert.Single(context.StaffLeaveEntries);

            controller.ModelState.Clear();
            var overlap = Entry(staff.Id, LeaveDay);
            overlap.ToDate = LeaveDay.AddDays(1).ToString("yyyy-MM-dd");
            Bind(controller, overlap);
            await controller.Record(overlap);
            Assert.Single(context.StaffLeaveEntries);

            controller.ModelState.Clear();
            var overBalance = Entry(staff.Id, LeaveDay.AddDays(1));
            Bind(controller, overBalance);
            await controller.Record(overBalance);
            Assert.Single(context.StaffLeaveEntries);
        }
    }

    [Fact]
    public async Task Record_rejects_dates_outside_current_year_and_uses_the_date_range_for_days()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 5m
            });
            await context.SaveChangesAsync();
            var outsideYear = Entry(staff.Id, LeaveDay.AddYears(-1));
            Bind(controller, outsideYear);
            await controller.Record(outsideYear);
            Assert.Empty(context.StaffLeaveEntries);

            controller.ModelState.Clear();
            var twoDays = Entry(staff.Id, LeaveDay, 0.5m);
            twoDays.ToDate = LeaveDay.AddDays(1).ToString("yyyy-MM-dd");
            Bind(controller, twoDays);
            Assert.IsType<RedirectToActionResult>(await controller.Record(twoDays));
            Assert.Equal(2m, (await context.StaffLeaveEntries.SingleAsync()).Days);
        }
    }

    [Fact]
    public async Task Record_accepts_past_today_and_future_leave_dates_within_current_year()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            context.LeaveEntitlements.Add(new LeaveEntitlement
            {
                StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = 5m
            });
            await context.SaveChangesAsync();
            var backDated = Entry(staff.Id, BackDay);
            Bind(controller, backDated);
            Assert.IsType<RedirectToActionResult>(await controller.Record(backDated));
            Assert.Equal(BackDay, (await context.StaffLeaveEntries.SingleAsync()).FromDate);
            context.StaffLeaveEntries.RemoveRange(context.StaffLeaveEntries);
            await context.SaveChangesAsync();

            controller.ModelState.Clear();
            var today = Entry(staff.Id, DateTime.Today);
            Bind(controller, today);
            Assert.IsType<RedirectToActionResult>(await controller.Record(today));
            Assert.Equal(DateTime.Today, (await context.StaffLeaveEntries.SingleAsync()).FromDate);
            context.StaffLeaveEntries.RemoveRange(context.StaffLeaveEntries);
            await context.SaveChangesAsync();

            controller.ModelState.Clear();
            var upcoming = Entry(staff.Id, DateTime.Today.AddDays(1), 0.5m);
            upcoming.ToDate = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");
            Bind(controller, upcoming);
            Assert.IsType<RedirectToActionResult>(await controller.Record(upcoming));
            var saved = await context.StaffLeaveEntries.SingleAsync();
            Assert.Equal(DateTime.Today.AddDays(1), saved.FromDate);
            Assert.Equal(DateTime.Today.AddDays(2), saved.ToDate);
            Assert.Equal(2m, saved.Days);
        }
    }

    private static AttendanceRecord Mark(Staff staff, DateTime day, string status, bool withoutPay = false) => new()
    {
        StaffId = staff.Id, AttendanceDate = day, Status = status, LeaveWithoutPay = withoutPay
    };

    private static void AddQuota(IMSBContext context, Staff staff, decimal quota) => context.LeaveEntitlements.Add(new LeaveEntitlement
    {
        StaffId = staff.Id, StaffCategory = "Operators", LeaveType = "Casual", Year = Year, AnnualQuota = quota, IsAttendanceLeaveType = true
    });

    private string? ErrorOf(LeavesController controller) => controller.TempData["LeaveError"] as string;

    [Fact]
    public async Task Back_dated_leave_cannot_start_before_the_join_date()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            staff.JoinDate = BackDay.AddDays(1);
            AddQuota(context, staff, 5m);
            await context.SaveChangesAsync();
            var form = Entry(staff.Id, BackDay);
            Bind(controller, form);

            await controller.Record(form);

            Assert.Empty(context.StaffLeaveEntries);
            Assert.False(string.IsNullOrEmpty(ErrorOf(controller)));
        }
    }

    [Theory]
    [InlineData(AttendanceStatuses.Present)]
    [InlineData(AttendanceStatuses.HalfDay)]
    public async Task Back_dated_leave_is_rejected_on_a_day_marked_as_worked(string status)
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            AddQuota(context, staff, 5m);
            context.AttendanceRecords.Add(Mark(staff, BackDay, status));
            await context.SaveChangesAsync();
            var form = Entry(staff.Id, BackDay);
            Bind(controller, form);

            await controller.Record(form);

            Assert.Empty(context.StaffLeaveEntries);
            Assert.False(string.IsNullOrEmpty(ErrorOf(controller)));
        }
    }

    [Fact]
    public async Task Back_dated_leave_makes_a_day_marked_without_pay_paid_and_deleting_it_reverses_that()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            AddQuota(context, staff, 1m);
            var usesQuota = Mark(staff, BackDay.AddDays(1), AttendanceStatuses.Leave);
            var unpaid = Mark(staff, BackDay.AddDays(2), AttendanceStatuses.Leave, withoutPay: true);
            context.AttendanceRecords.AddRange(usesQuota, unpaid);
            await context.SaveChangesAsync();
            context.LeaveEntitlements.Single().AnnualQuota = 2m;   // room for this entry
            await context.SaveChangesAsync();
            var form = Entry(staff.Id, BackDay.AddDays(2));
            Bind(controller, form);

            Assert.IsType<RedirectToActionResult>(await controller.Record(form));
            Assert.False((await context.AttendanceRecords.AsNoTracking().SingleAsync(r => r.Id == unpaid.Id)).LeaveWithoutPay);

            context.LeaveEntitlements.Single().AnnualQuota = 1m;
            await context.SaveChangesAsync();
            Assert.IsType<RedirectToActionResult>(await controller.DeleteEntry((await context.StaffLeaveEntries.SingleAsync()).Id));
            Assert.True((await context.AttendanceRecords.AsNoTracking().SingleAsync(r => r.Id == unpaid.Id)).LeaveWithoutPay);
        }
    }

    [Fact]
    public async Task Back_dated_leave_on_a_day_marked_leave_counts_that_day_once()
    {
        var (context, controller, staff) = Create();
        using (context)
        {
            AddQuota(context, staff, 1m);
            context.AttendanceRecords.Add(Mark(staff, BackDay, AttendanceStatuses.Leave));
            await context.SaveChangesAsync();
            var form = Entry(staff.Id, BackDay);
            Bind(controller, form);

            Assert.IsType<RedirectToActionResult>(await controller.Record(form));

            Assert.Single(context.StaffLeaveEntries);
            Assert.Equal(0m, Assert.Single(((LeaveManagementViewModel)Assert.IsType<ViewResult>(await controller.Index()).Model!).Balances).Balance);
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
