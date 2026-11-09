using System.Security.Claims;
using System.Text.Json;
using IMSB.Admin.Controllers;
using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

public class OtAssignmentsControllerTests
{
    private static OtAssignmentsController Controller(IMSBContext context) =>
        new(new IMSB.Backend.OtAssignments(context), TestHelpers.LocalizerFactory())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "admin@imsb"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "Test"))
                }
            }
        };

    private static (IMSBContext Context, Staff Staff) Seed(string status = "ACTIVE")
    {
        var context = TestHelpers.CreateContext();
        var staff = TestHelpers.ExistingStaff("EMP701", "Ot Tester", "900000701", status: status);
        context.Staff.Add(staff);
        context.SaveChanges();
        return (context, staff);
    }

    private static readonly DateTime Today = DateTime.Today;

    /// <summary>yyyy-MM-ddTHH:mm on the day <paramref name="days"/> after today.</summary>
    private static string At(int days, string time) => Today.AddDays(days).ToString("yyyy-MM-dd") + "T" + time;

    /// <summary>Today 18:00 – 21:30.</summary>
    private static OtAssignmentDto Form(Guid? staffId) => Form(staffId, At(0, "18:00"), At(0, "21:30"));

    private static OtAssignmentDto Form(Guid? staffId, string? from, string? to) =>
        new() { StaffId = staffId, FromDateTime = from, ToDateTime = to };

    // ── Add ───────────────────────────────────────────────────────
    [Fact]
    public async Task Create_saves_the_timings_for_the_staff_member()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var result = TestHelpers.OkJson(await Controller(context).Create(Form(staff.Id)));
            Assert.Equal("OT assignment saved.", result.GetProperty("message").GetString());

            var saved = await context.OtAssignments.SingleAsync();
            Assert.Equal(staff.Id, saved.StaffId);
            Assert.Equal(Today.AddHours(18), saved.FromDateTime);
            Assert.Equal(Today.AddHours(21.5), saved.ToDateTime);
            Assert.Equal("admin@imsb", saved.CreatedBy);
        }
    }

    [Fact]
    public async Task Create_accepts_a_window_running_past_midnight()
    {
        var (context, staff) = Seed();
        using (context)
        {
            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id, At(0, "22:00"), At(1, "02:00"))));

            var saved = await context.OtAssignments.SingleAsync();
            Assert.Equal(Today.AddHours(22), saved.FromDateTime);
            Assert.Equal(Today.AddDays(1).AddHours(2), saved.ToDateTime);
        }
    }

    [Fact]
    public async Task Create_refuses_a_second_assignment_for_the_same_staff_member()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));

            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(0, "06:00"), At(0, "07:00"))));

            Assert.Contains("already has an OT assignment", errors["staff_id"]);
            Assert.Equal(Today.AddHours(18), (await context.OtAssignments.SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task Create_refuses_an_unknown_staff_member()
    {
        var (context, _) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(Guid.NewGuid())));

            Assert.Equal("Staff member not found", errors["staff_id"]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_needs_a_staff_member(bool emptyGuid)
    {
        var (context, _) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(emptyGuid ? Guid.Empty : null)));

            Assert.Equal("Select a staff member", errors["staff_id"]);
        }
    }

    [Theory]
    [InlineData(null, "2026-11-05T21:00", "from_datetime", "Enter both OT From and OT To dates and times")]
    [InlineData("2026-11-05T18:00", null, "to_datetime", "Enter both OT From and OT To dates and times")]
    [InlineData("2026-11-05T18:00", "2026-11-05T18:00", "to_datetime", "OT To time must be later than OT From time")]
    [InlineData("2026-11-05T21:00", "2026-11-05T18:00", "to_datetime", "OT To time must be later than OT From time")]
    [InlineData("2026-11-05T25:00", "2026-11-05T21:00", "from_datetime", "Enter a valid date and time")]
    public async Task Create_rejects_invalid_timings(string? from, string? to, string field, string message)
    {
        var (context, staff) = Seed();
        using (context)
        {
            // the cases are written for 5 Nov 2026; they run on today's date so the date rules pass
            string? OnToday(string? v) => v?.Replace("2026-11-05", Today.ToString("yyyy-MM-dd"));
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, OnToday(from), OnToday(to))));

            Assert.Equal(message, errors[field]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Fact]
    public async Task Create_without_a_body_is_rejected()
    {
        var (context, _) = Seed();
        using (context) Assert.IsType<BadRequestObjectResult>(await Controller(context).Create(null));
    }

    [Fact]
    public async Task Create_refuses_a_date_before_today()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(-1, "18:00"), At(-1, "21:00"))));

            Assert.Equal("The OT date cannot be before today", errors["ot_date"]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Fact]
    public async Task Create_refuses_a_date_in_the_next_month_and_accepts_the_last_day_of_this_one()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var monthEnd = OtAssignmentMapper.MonthEnd(Today);
            var lastDay = (int)(monthEnd - Today).TotalDays;

            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(lastDay + 1, "18:00"), At(lastDay + 1, "21:00"))));
            Assert.Equal("The OT date must be within the current month", errors["ot_date"]);

            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id, At(lastDay, "22:00"), At(lastDay + 1, "01:00"))));
            Assert.Equal(monthEnd.AddHours(22), (await context.OtAssignments.SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task Create_refuses_a_window_of_24_hours_or_more()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(0, "18:00"), At(1, "18:00"))));

            Assert.Equal("OT must end within 24 hours of its start", errors["to_datetime"]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Fact]
    public async Task A_past_assignment_does_not_stop_a_new_one_for_the_same_staff_member()
    {
        var (context, staff) = Seed();
        using (context)
        {
            context.OtAssignments.Add(new OtAssignment { StaffId = staff.Id, FromDateTime = Today.AddDays(-3).AddHours(18), ToDateTime = Today.AddDays(-3).AddHours(20) });
            await context.SaveChangesAsync();

            var options = TestHelpers.OkJson(await Controller(context).Data()).GetProperty("staff");
            Assert.Equal(staff.Id, Assert.Single(options.EnumerateArray()).GetProperty("id").GetGuid());

            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id)));
            Assert.Equal(2, await context.OtAssignments.CountAsync());
            Assert.Equal(0, TestHelpers.OkJson(await Controller(context).Data()).GetProperty("staff").GetArrayLength());
        }
    }

    // ── OT Hrs saved in ot_assignments ───────────────────────────
    [Fact]
    public async Task Create_saves_the_ot_hours()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));   // 18:00-21:30

            Assert.Equal(3.5m, (await context.OtAssignments.SingleAsync()).OtHours);
        }
    }

    [Fact]
    public async Task Create_saves_the_ot_hours_of_a_window_past_midnight()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id, At(0, "22:00"), At(1, "02:15")));

            Assert.Equal(4.25m, (await context.OtAssignments.SingleAsync()).OtHours);
        }
    }

    [Fact]
    public async Task Update_saves_the_new_ot_hours()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            await Controller(context).Update(id, Form(null, At(0, "17:00"), At(0, "23:00")));

            Assert.Equal(6m, (await context.OtAssignments.AsNoTracking().SingleAsync()).OtHours);
        }
    }

    [Fact]
    public async Task A_refused_update_keeps_the_saved_ot_hours()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            TestHelpers.Errors(await Controller(context).Update(id, Form(null, At(0, "15:00"), At(0, "23:00"))));   // inside the shift

            Assert.Equal(3.5m, (await context.OtAssignments.AsNoTracking().SingleAsync()).OtHours);
        }
    }

    [Fact]
    public async Task Ot_assignment_list_shows_the_saved_ot_hours()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));

            var row = Assert.Single(TestHelpers.OkJson(await Controller(context).Data()).GetProperty("rows").EnumerateArray());

            Assert.Equal(3.5m, row.GetProperty("otHours").GetDecimal());
        }
    }

    // ── Shift rule (the seeded MORNING shift is "Morning (8AM-4PM)") ──
    [Fact]
    public async Task Create_refuses_ot_that_starts_before_the_shift_ends()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(0, "14:00"), At(0, "18:00"))));

            Assert.Equal("OT can start only after the employee's shift ends", errors["from_datetime"]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Fact]
    public async Task Create_refuses_ot_that_runs_into_the_next_shift()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, At(0, "22:00"), At(1, "09:00"))));

            Assert.Equal("OT must end before the employee's next shift starts", errors["to_datetime"]);
            Assert.Empty(context.OtAssignments);
        }
    }

    [Fact]
    public async Task Create_accepts_ot_from_the_shift_end_to_the_next_shift_start()
    {
        var (context, staff) = Seed();
        using (context)
        {
            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id, At(0, "16:00"), At(1, "08:00"))));
            Assert.Equal(Today.AddHours(16), (await context.OtAssignments.SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task Update_refuses_moving_ot_into_the_shift()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            var errors = TestHelpers.Errors(await Controller(context).Update(id, Form(null, At(0, "15:00"), At(0, "17:00"))));

            Assert.Equal("OT can start only after the employee's shift ends", errors["from_datetime"]);
            Assert.Equal(Today.AddHours(18), (await context.OtAssignments.AsNoTracking().SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task A_shift_without_readable_timings_does_not_restrict_ot()
    {
        var (context, staff) = Seed();
        using (context)
        {
            staff.Shift = "Night";
            await context.SaveChangesAsync();

            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id, At(0, "10:00"), At(0, "12:00"))));
        }
    }

    [Fact]
    public async Task Data_gives_the_shift_and_its_timings_of_each_employee()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var other = TestHelpers.ExistingStaff("EMP702", "Evening Person", "900000702", shift: "EVENING");
            context.Staff.Add(other);
            await context.SaveChangesAsync();
            await Controller(context).Create(Form(staff.Id));

            var data = TestHelpers.OkJson(await Controller(context).Data());

            var option = Assert.Single(data.GetProperty("staff").EnumerateArray());
            Assert.Equal("Evening (4PM-10PM)", option.GetProperty("shiftName").GetString());
            Assert.Equal("16:00", option.GetProperty("shiftStart").GetString());
            Assert.Equal("22:00", option.GetProperty("shiftEnd").GetString());
            var row = Assert.Single(data.GetProperty("rows").EnumerateArray());
            Assert.Equal("Morning (8AM-4PM)", row.GetProperty("shiftName").GetString());
            Assert.Equal(("08:00", "16:00"), (row.GetProperty("shiftStart").GetString(), row.GetProperty("shiftEnd").GetString()));
        }
    }

    [Theory]
    [InlineData("08:00", "16:00", "16:00", "20:00", null)]
    [InlineData("08:00", "16:00", "15:59", "20:00", "ValOtBeforeShiftEnd")]
    [InlineData("08:00", "16:00", "07:00", "08:00", "ValOtBeforeShiftEnd")]       // before the shift on the same day
    [InlineData("08:00", "16:00", "23:00", "+08:00", null)]                       // up to the next morning's start
    [InlineData("08:00", "16:00", "23:00", "+08:01", "ValOtIntoNextShift")]
    [InlineData("22:00", "06:00", "06:00", "10:00", null)]                        // overnight shift: OT after it ends in the morning
    [InlineData("22:00", "06:00", "05:00", "07:00", "ValOtBeforeShiftEnd")]
    [InlineData("22:00", "06:00", "18:00", "22:00", null)]
    [InlineData("22:00", "06:00", "18:00", "23:00", "ValOtIntoNextShift")]
    public void ShiftConflict_keeps_ot_between_the_shift_end_and_the_next_shift(string shiftStart, string shiftEnd, string from, string to, string? expected)
    {
        var day = new DateTime(2026, 11, 5);
        DateTime On(string time) => time.StartsWith('+') ? day.AddDays(1) + TimeSpan.Parse(time[1..]) : day + TimeSpan.Parse(time);

        Assert.Equal(expected, OtAssignmentMapper.ShiftConflict(On(from), On(to), (TimeSpan.Parse(shiftStart), TimeSpan.Parse(shiftEnd))));
    }

    [Fact]
    public void ShiftConflict_is_not_checked_without_shift_timings() =>
        Assert.Null(OtAssignmentMapper.ShiftConflict(new DateTime(2026, 11, 5, 9, 0, 0), new DateTime(2026, 11, 5, 10, 0, 0), null));

    // ── View ──────────────────────────────────────────────────────
    [Fact]
    public async Task Data_lists_assignments_with_staff_details()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));

            var row = Assert.Single(TestHelpers.OkJson(await Controller(context).Data()).GetProperty("rows").EnumerateArray());

            Assert.Equal("EMP701", row.GetProperty("employeeId").GetString());
            Assert.Equal("Ot Tester", row.GetProperty("staffName").GetString());
            Assert.Equal("Sales", row.GetProperty("staffCategory").GetString());
            Assert.Equal("Executive", row.GetProperty("designation").GetString());
            Assert.Equal(At(0, "18:00"), row.GetProperty("fromDateTime").GetString());
            Assert.Equal(At(0, "21:30"), row.GetProperty("toDateTime").GetString());
            Assert.Equal("admin@imsb", row.GetProperty("by").GetString());
            Assert.Equal(Today.ToString("yyyy-MM-dd"), TestHelpers.OkJson(await Controller(context).Data()).GetProperty("today").GetString());
        }
    }

    [Fact]
    public async Task Data_offers_only_active_staff_without_an_assignment()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var unassigned = TestHelpers.ExistingStaff("EMP702", "Waiting Person", "900000702");
            var inactive = TestHelpers.ExistingStaff("EMP703", "Former Person", "900000703", status: "INACTIVE");
            context.Staff.AddRange(unassigned, inactive);
            await context.SaveChangesAsync();
            await Controller(context).Create(Form(staff.Id));

            var options = TestHelpers.OkJson(await Controller(context).Data()).GetProperty("staff");

            var option = Assert.Single(options.EnumerateArray());
            Assert.Equal(unassigned.Id, option.GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task Data_is_empty_without_assignments()
    {
        var (context, _) = Seed();
        using (context)
            Assert.Equal(0, TestHelpers.OkJson(await Controller(context).Data()).GetProperty("rows").GetArrayLength());
    }

    // ── Update ────────────────────────────────────────────────────
    [Fact]
    public async Task Update_changes_the_timings_and_records_who()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            Assert.IsType<OkObjectResult>(await Controller(context).Update(id, Form(null, At(0, "19:15"), At(0, "22:45"))));

            var saved = await context.OtAssignments.SingleAsync();
            Assert.Equal(Today.AddHours(19.25), saved.FromDateTime);
            Assert.Equal(Today.AddHours(22.75), saved.ToDateTime);
            Assert.Equal("admin@imsb", saved.UpdatedBy);
            Assert.NotNull(saved.UpdatedAt);
        }
    }

    [Fact]
    public async Task Update_cannot_move_the_assignment_to_another_staff_member()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            await Controller(context).Update(id, Form(Guid.NewGuid(), At(0, "19:00"), At(0, "20:00")));

            Assert.Equal(staff.Id, (await context.OtAssignments.SingleAsync()).StaffId);
        }
    }

    [Fact]
    public async Task Update_rejects_invalid_timings_and_keeps_the_saved_ones()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            var errors = TestHelpers.Errors(await Controller(context).Update(id, Form(null, At(0, "22:00"), At(0, "02:00"))));

            Assert.Equal("OT To time must be later than OT From time", errors["to_datetime"]);
            Assert.Equal(Today.AddHours(18), (await context.OtAssignments.AsNoTracking().SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task Update_refuses_an_assignment_dated_before_today_and_keeps_it()
    {
        var (context, staff) = Seed();
        using (context)
        {
            var past = new OtAssignment { StaffId = staff.Id, FromDateTime = Today.AddDays(-1).AddHours(18), ToDateTime = Today.AddDays(-1).AddHours(20) };
            context.OtAssignments.Add(past);
            await context.SaveChangesAsync();

            var errors = TestHelpers.Errors(await Controller(context).Update(past.Id, Form(null)));

            Assert.Equal("OT assignments for past dates cannot be edited", errors["request"]);
            var saved = await context.OtAssignments.AsNoTracking().SingleAsync();
            Assert.Equal(Today.AddDays(-1).AddHours(18), saved.FromDateTime);
            Assert.Null(saved.UpdatedBy);
        }
    }

    [Fact]
    public async Task Update_cannot_move_an_assignment_to_a_date_before_today()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            var errors = TestHelpers.Errors(await Controller(context).Update(id, Form(null, At(-1, "18:00"), At(-1, "20:00"))));

            Assert.Equal("The OT date cannot be before today", errors["ot_date"]);
            Assert.Equal(Today.AddHours(18), (await context.OtAssignments.AsNoTracking().SingleAsync()).FromDateTime);
        }
    }

    [Fact]
    public async Task Update_of_an_unknown_assignment_is_not_found()
    {
        var (context, _) = Seed();
        using (context) Assert.IsType<NotFoundResult>(await Controller(context).Update(Guid.NewGuid(), Form(null)));
    }

    // ── Delete ────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_removes_it_and_the_staff_member_can_be_assigned_again()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));
            var id = (await context.OtAssignments.SingleAsync()).Id;

            var result = TestHelpers.OkJson(await Controller(context).Delete(id));

            Assert.Equal("OT assignment deleted.", result.GetProperty("message").GetString());
            Assert.Empty(context.OtAssignments);
            var options = TestHelpers.OkJson(await Controller(context).Data()).GetProperty("staff");
            Assert.Equal(staff.Id, Assert.Single(options.EnumerateArray()).GetProperty("id").GetGuid());
            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id)));
        }
    }

    [Fact]
    public async Task Delete_of_an_unknown_assignment_is_not_found()
    {
        var (context, _) = Seed();
        using (context) Assert.IsType<NotFoundResult>(await Controller(context).Delete(Guid.NewGuid()));
    }

    // ── Time rules ────────────────────────────────────────────────
    [Theory]
    [InlineData("2026-11-05T18:00", "2026-11-05T21:00", null)]
    [InlineData("2026-11-05T18:00", "2026-11-05T18:01", null)]
    [InlineData("2026-11-05T22:00", "2026-11-06T02:00", null)]   // past midnight, into the next day
    [InlineData("2026-11-30T22:00", "2026-12-01T02:00", null)]   // last day of the month, ending in the next one
    [InlineData("2026-11-30T23:00", "2026-12-01T22:59", null)]
    [InlineData("2026-11-04T18:00", "2026-11-04T21:00", "ot_date:ValOtDatePast")]
    [InlineData("2026-11-04T22:00", "2026-11-05T02:00", "ot_date:ValOtDatePast")]   // started yesterday
    [InlineData("2026-12-01T18:00", "2026-12-01T21:00", "ot_date:ValOtDateThisMonth")]
    [InlineData("2026-12-31T22:00", "2027-01-01T02:00", "ot_date:ValOtDateThisMonth")]
    [InlineData("2026-11-05T18:00", "2026-11-06T18:00", "to_datetime:ValOtOneDay")]
    [InlineData("2026-11-05T18:00", "2026-11-07T17:00", "to_datetime:ValOtOneDay")]
    [InlineData(null, null, "from_datetime:ValOtTimes")]
    [InlineData("", "", "from_datetime:ValOtTimes")]
    [InlineData("2026-11-05T18:00", null, "to_datetime:ValOtTimes")]
    [InlineData(null, "2026-11-05T21:00", "from_datetime:ValOtTimes")]
    [InlineData("2026-11-05T18:00", "2026-11-05T18:00", "to_datetime:ValOtToAfterFrom")]
    [InlineData("2026-11-05T21:00", "2026-11-05T18:00", "to_datetime:ValOtToAfterFrom")]
    [InlineData("2026-11-06T18:00", "2026-11-05T21:00", "to_datetime:ValOtToAfterFrom")]   // later time, earlier day
    [InlineData("18:00", "2026-11-05T21:00", "from_datetime:ValDateTime")]   // time without a date
    [InlineData("2026-11-05", "2026-11-05T21:00", "from_datetime:ValDateTime")]   // date without a time
    [InlineData("2026-11-05T18:00", "21:00", "to_datetime:ValDateTime")]
    [InlineData("2026-02-30T18:00", "2026-03-01T21:00", "from_datetime:ValDateTime")]
    public void ValidateTimes_needs_this_months_date_from_today_with_to_after_from(string? from, string? to, string? expected)
    {
        var error = new OtAssignmentDto { FromDateTime = from, ToDateTime = to }.ValidateTimes(new DateTime(2026, 11, 5));
        Assert.Equal(expected, error == null ? null : $"{error.Value.Field}:{error.Value.Key}");
    }
}
