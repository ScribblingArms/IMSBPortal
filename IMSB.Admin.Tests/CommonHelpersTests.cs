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
    [InlineData(new string[0], "APP-0001")]
    [InlineData(new[] { "APP-0001", "APP-0007", "APP-0003" }, "APP-0008")]  // after the highest, not the last
    [InlineData(new[] { "APP-9999" }, "APP-10000")]                         // grows past the width
    [InlineData(new[] { "LEGACY", "APP-0002" }, "APP-0003")]                // codes without a number are skipped
    public void NextCode_follows_the_highest_number(string[] existing, string expected) =>
        Assert.Equal(expected, NextCode(existing, "APP-", 4));

    [Fact]
    public void NextCode_pads_to_the_width() => Assert.Equal("EMP007", NextCode(new[] { "EMP006" }, "EMP", 3));

    [Theory]
    [InlineData("FullName", "full_name")]
    [InlineData("dto.DateOfBirth", "date_of_birth")]
    [InlineData("Items[2].Quantity", "items_2_quantity")]
    [InlineData("$.items", "items")]
    public void ToErrorKey_gives_the_form_field_names(string key, string expected) => Assert.Equal(expected, ToErrorKey(key));

    [Theory]
    [InlineData(null, "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]   // formula injection
    [InlineData("-1", "'-1")]
    public void CsvEscape_quotes_and_neutralises_formulas(string? input, string expected) => Assert.Equal(expected, CsvEscape(input));

    [Theory]
    [InlineData(2026, 11, true)]
    [InlineData(1999, 1, false)]
    [InlineData(2026, 13, false)]
    [InlineData(null, 5, false)]
    [InlineData(2026, null, false)]
    public void TryMonthStart_checks_the_range(int? year, int? month, bool ok)
    {
        Assert.Equal(ok, TryMonthStart(year, month, out var start));
        if (ok) Assert.Equal(new DateTime(year!.Value, month!.Value, 1), start);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  late  arrival ", "late  arrival")]   // inner spacing kept, unlike Clean
    public void TrimOrNull_trims_and_nulls_blanks(string? input, string? expected) => Assert.Equal(expected, TrimOrNull(input));

    [Fact]
    public void Time_formats_hh_mm()
    {
        Assert.Equal("06:05", Time(new TimeSpan(6, 5, 59)));
        Assert.Null(Time(null));
    }

    [Theory]
    [InlineData("2026-11-11", true)]
    [InlineData(" 2026-11-11 ", true)]
    [InlineData("11/11/2026", false)]
    [InlineData(null, false)]
    public void ParseDate_reads_yyyy_MM_dd(string? value, bool ok) =>
        Assert.Equal(ok ? new DateTime(2026, 11, 11) : null, ParseDate(value));

    [Fact]
    public void TryParseDay_defaults_to_today()
    {
        Assert.True(TryParseDay(null, out var today));
        Assert.Equal(DateTime.Today, today);
        Assert.True(TryParseDay("2026-01-31", out var day));
        Assert.Equal(new DateTime(2026, 1, 31), day);
        Assert.False(TryParseDay("31-01-2026", out _));
    }

    [Fact]
    public void TryResolveAttendanceMonth_allows_the_last_ten_years()
    {
        var thisYear = DateTime.Today.Year;
        Assert.True(TryResolveAttendanceMonth(null, null, out var current));
        Assert.Equal(new DateTime(thisYear, DateTime.Today.Month, 1), current);
        Assert.True(TryResolveAttendanceMonth(thisYear - 10, 1, out _));
        Assert.False(TryResolveAttendanceMonth(thisYear - 11, 1, out _));
        Assert.False(TryResolveAttendanceMonth(thisYear + 1, 1, out _));
        Assert.False(TryResolveAttendanceMonth(thisYear, 13, out _));
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("2026-11-01", "2026-11-30", "")]
    [InlineData("2026-11-30", "2026-11-01", "to:ValDateRange")]
    [InlineData("bad", "2026-11-01", "from:ValDateInvalid")]
    [InlineData("2026-11-01", "bad", "to:ValDateInvalid")]
    public void ValidateRange_reports_bad_and_reversed_days(string? from, string? to, string expected)
    {
        var errors = ValidateRange(from, to, new KeyLocalizer(), out var fromDay, out var toDay);
        Assert.Equal(expected, string.Join(",", errors.Select(e => $"{e.Key}:{e.Value}")));
        if (expected == "" && from != null) Assert.Equal(DateTime.Parse(from), fromDay);
        if (expected == "" && to != null) Assert.Equal(DateTime.Parse(to), toDay);
    }

    [Fact]
    public void ResolvePeriod_uses_days_then_month_then_this_month()
    {
        var l = new KeyLocalizer();
        Assert.Empty(ResolvePeriod(null, "2026-11-05", null, l, out var s1, out var e1));
        Assert.Equal((new DateTime(2026, 11, 5), new DateTime(2026, 11, 5)), (s1, e1));      // one day alone
        Assert.Empty(ResolvePeriod("2026-02", null, null, l, out var s2, out var e2));
        Assert.Equal((new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)), (s2, e2));
        Assert.Empty(ResolvePeriod(null, null, null, l, out var s3, out _));
        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), s3);
        Assert.Equal("ValDateInvalid", ResolvePeriod("2026-13", null, null, l, out _, out _)["month"]);
        Assert.Equal("ValDateRange", ResolvePeriod(null, "2026-11-30", "2026-11-01", l, out _, out _)["to"]);
    }

    [Fact]
    public void GuidClaim_reads_a_guid_or_null()
    {
        var id = Guid.NewGuid();
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim("sid", id.ToString()),
            new System.Security.Claims.Claim("bad", "not-a-guid")
        }));
        Assert.Equal(id, IMSB.Common.ClaimsPrincipalExtensions.GuidClaim(user, "sid"));
        Assert.Null(IMSB.Common.ClaimsPrincipalExtensions.GuidClaim(user, "bad"));
        Assert.Null(IMSB.Common.ClaimsPrincipalExtensions.GuidClaim(user, "missing"));
    }

    /// <summary>Returns the resource key itself, so the tests see which message was chosen.</summary>
    private sealed class KeyLocalizer : Microsoft.Extensions.Localization.IStringLocalizer
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] => new(name, name);
        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
