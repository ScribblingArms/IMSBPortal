using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Tests;

public class CommonHelpersTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-11-01", null, null)]
    [InlineData(null, "2026-11-30", null)]
    [InlineData("2026-11-01", "2026-11-01", null)]           // same day is allowed
    [InlineData("2026-11-01", "2026-11-30", null)]
    [InlineData("2026-11-30", "2026-11-01", "to_date:ValDateRange")]
    [InlineData("01/11/2026", null, "from_date:ValDateInvalid")]
    [InlineData("2026-11-01", "2026-02-30", "to_date:ValDateInvalid")]
    public void ParsePeriod_accepts_optional_dates_with_to_not_before_from(string? from, string? to, string? expected)
    {
        var error = ParsePeriod(from, to, out var fromDate, out var toDate);
        Assert.Equal(expected, error == null ? null : $"{error.Value.Field}:{error.Value.Key}");
        if (expected == null)
        {
            Assert.Equal(from == null ? null : DateTime.Parse(from), fromDate);
            Assert.Equal(to == null ? null : DateTime.Parse(to), toDate);
        }
    }

    [Theory]
    [InlineData("00:00", true)]
    [InlineData("23:59", true)]
    [InlineData("24:00", false)]
    [InlineData("9:30", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryParseTime_reads_hh_mm(string? value, bool ok) => Assert.Equal(ok, TryParseTime(value, out _));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", null)]
    [InlineData("18:00", "21:00", null)]
    [InlineData("18:00", "18:01", null)]
    [InlineData("18:00", null, "ot_to_time:ValOtTimes")]
    [InlineData(null, "21:00", "ot_from_time:ValOtTimes")]
    [InlineData("18:00", "18:00", "ot_to_time:ValOtToAfterFrom")]
    [InlineData("21:00", "18:00", "ot_to_time:ValOtToAfterFrom")]
    [InlineData("22:00", "02:00", "ot_to_time:ValOtToAfterFrom")]   // no window past midnight
    [InlineData("6pm", "21:00", "ot_from_time:ValTime")]
    public void ValidateOvertime_needs_both_times_with_to_after_from(string? from, string? to, string? expected)
    {
        var dto = new CreateStaffDto { OtFromTime = from, OtToTime = to };
        var error = dto.ValidateOvertime();
        Assert.Equal(expected, error == null ? null : $"{error.Value.Field}:{error.Value.Key}");
    }

    [Fact]
    public void Ot_timings_are_listed_only_with_salary_access()
    {
        var staff = new Staff { EmployeeId = "EMP001", OtFromTime = new TimeSpan(18, 0, 0), OtToTime = new TimeSpan(21, 30, 0) };
        var names = new Dictionary<Guid, string>();
        Assert.Equal(("18:00", "21:30"), (staff.ToListItem(names, includeSalary: true).OtFromTime, staff.ToListItem(names, includeSalary: true).OtToTime));
        Assert.Null(staff.ToListItem(names).OtFromTime);
    }
}
