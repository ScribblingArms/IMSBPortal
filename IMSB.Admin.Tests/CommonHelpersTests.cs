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
}
