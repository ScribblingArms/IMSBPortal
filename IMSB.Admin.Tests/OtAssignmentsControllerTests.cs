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

    private static OtAssignmentDto Form(Guid? staffId, string? from = "2026-11-05T18:00", string? to = "2026-11-05T21:30") =>
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
            Assert.Equal(new DateTime(2026, 11, 5, 18, 0, 0), saved.FromDateTime);
            Assert.Equal(new DateTime(2026, 11, 5, 21, 30, 0), saved.ToDateTime);
            Assert.Equal("admin@imsb", saved.CreatedBy);
        }
    }

    [Fact]
    public async Task Create_accepts_a_window_running_past_midnight()
    {
        var (context, staff) = Seed();
        using (context)
        {
            Assert.IsType<OkObjectResult>(await Controller(context).Create(Form(staff.Id, "2026-11-05T22:00", "2026-11-06T02:00")));

            var saved = await context.OtAssignments.SingleAsync();
            Assert.Equal(new DateTime(2026, 11, 5, 22, 0, 0), saved.FromDateTime);
            Assert.Equal(new DateTime(2026, 11, 6, 2, 0, 0), saved.ToDateTime);
        }
    }

    [Fact]
    public async Task Create_refuses_a_second_assignment_for_the_same_staff_member()
    {
        var (context, staff) = Seed();
        using (context)
        {
            await Controller(context).Create(Form(staff.Id));

            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, "2026-11-06T06:00", "2026-11-06T07:00")));

            Assert.Contains("already has an OT assignment", errors["staff_id"]);
            Assert.Equal(new DateTime(2026, 11, 5, 18, 0, 0), (await context.OtAssignments.SingleAsync()).FromDateTime);
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
            var errors = TestHelpers.Errors(await Controller(context).Create(Form(staff.Id, from, to)));

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
            Assert.Equal("2026-11-05T18:00", row.GetProperty("fromDateTime").GetString());
            Assert.Equal("2026-11-05T21:30", row.GetProperty("toDateTime").GetString());
            Assert.Equal("admin@imsb", row.GetProperty("by").GetString());
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

            Assert.IsType<OkObjectResult>(await Controller(context).Update(id, Form(null, "2026-11-07T19:15", "2026-11-07T22:45")));

            var saved = await context.OtAssignments.SingleAsync();
            Assert.Equal(new DateTime(2026, 11, 7, 19, 15, 0), saved.FromDateTime);
            Assert.Equal(new DateTime(2026, 11, 7, 22, 45, 0), saved.ToDateTime);
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

            await Controller(context).Update(id, Form(Guid.NewGuid(), "2026-11-05T19:00", "2026-11-05T20:00"));

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

            var errors = TestHelpers.Errors(await Controller(context).Update(id, Form(null, "2026-11-05T22:00", "2026-11-05T02:00")));

            Assert.Equal("OT To time must be later than OT From time", errors["to_datetime"]);
            Assert.Equal(new DateTime(2026, 11, 5, 18, 0, 0), (await context.OtAssignments.AsNoTracking().SingleAsync()).FromDateTime);
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
    [InlineData("2026-12-31T22:00", "2027-01-01T02:00", null)]
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
    public void ValidateTimes_needs_both_date_times_with_to_after_from(string? from, string? to, string? expected)
    {
        var error = new OtAssignmentDto { FromDateTime = from, ToDateTime = to }.ValidateTimes();
        Assert.Equal(expected, error == null ? null : $"{error.Value.Field}:{error.Value.Key}");
    }
}
