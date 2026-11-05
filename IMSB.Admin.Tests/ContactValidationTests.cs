using System.ComponentModel.DataAnnotations;
using System.Reflection;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>The mobile, email and Aadhaar rules shared by the Staff, Artisans and Applications forms.</summary>
public class ContactValidationTests
{
    private static string AdminFile(params string[] parts) => File.ReadAllText(Path.Combine(new[]
    {
        typeof(ContactValidationTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!
    }.Concat(parts).ToArray()));

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("6000000000", true)]
    [InlineData("987654321", false)]      // 9 digits
    [InlineData("98765432101", false)]    // 11 digits
    [InlineData("+919876543210", false)]
    [InlineData("09876543210", false)]
    [InlineData("98765 43210", false)]
    [InlineData("98765-43210", false)]
    [InlineData("98765abcde", false)]
    [InlineData("5876543210", false)]     // Indian mobile numbers start with 6-9
    [InlineData("9876543210\n", false)]
    [InlineData("", false)]
    public void Mobile_is_exactly_10_digits(string mobile, bool valid) => Assert.Equal(valid, MobileNumbers.IsValid(mobile));

    [Theory]
    [InlineData("name@example.com", true)]
    [InlineData("first.last+tag@mail.example.co.in", true)]
    [InlineData("user_01%x@my-domain.org", true)]
    [InlineData("name@example", false)]            // no top-level domain
    [InlineData("name@example.c", false)]          // top-level domain too short
    [InlineData("name@example.123", false)]
    [InlineData("name@-example.com", false)]
    [InlineData("name@example-.com", false)]
    [InlineData("name@example..com", false)]
    [InlineData(".name@example.com", false)]
    [InlineData("na..me@example.com", false)]
    [InlineData("name.@example.com", false)]
    [InlineData("name@@example.com", false)]
    [InlineData("na me@example.com", false)]
    [InlineData("@example.com", false)]
    [InlineData("name@", false)]
    [InlineData("not-an-email", false)]
    public void Email_follows_the_standard_format(string email, bool valid)
    {
        var dto = TestHelpers.ValidCreateDto(email: email);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        Assert.Equal(valid, !results.Any(r => r.MemberNames.Contains(nameof(CreateStaffDto.Email))));
    }

    [Theory]
    [InlineData("2341-2341-2346", true)]
    [InlineData("234123412346", true)]
    [InlineData(" 2341-2341-2346 ", true)]
    [InlineData("2341 2341 2346", true)]     // separators do not count towards the length
    [InlineData("2341-23412346", true)]
    [InlineData("23-4123-412346", true)]
    [InlineData("2341-2341-2347", true)]     // the digits themselves are not checked
    [InlineData("1111-1111-1111", true)]
    [InlineData("0000-0000-0000", true)]
    [InlineData("2341-2341-234", false)]     // 11 digits
    [InlineData("2341-2341-23461", false)]   // 13 digits
    [InlineData("abcd-efgh-ijkl", false)]
    [InlineData("2341-2341-234a", false)]    // 11 digits and a letter
    [InlineData("2341-2341-234a6", false)]   // 12 digits and a letter
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Aadhaar_only_has_to_be_12_digits(string? aadhaar, bool valid) => Assert.Equal(valid, Aadhaar.IsValid(aadhaar));

    [Fact]
    public void Hyphens_do_not_change_what_is_stored_for_a_number() =>
        Assert.Equal(Aadhaar.Hash("234123412346"), Aadhaar.Hash("2341-2341-2346"));

    // ── Browser copies ────────────────────────────────────────────
    [Fact]
    public void Browser_email_rule_is_identical_to_the_server_rule() =>
        Assert.Contains($"\"{EmailAddresses.Pattern}\"", AdminFile("wwwroot", "js", "main.js"));

    [Fact]
    public void Browser_aadhaar_rule_only_checks_the_length() =>
        Assert.Contains(@"aadhaar: v => /^[0-9]{12}$/.test((v || '').replace(/[-\s]/g, ''))", AdminFile("wwwroot", "js", "main.js"));

    [Theory]
    [InlineData("Staff", "staff.js", "staffMobile", "staffAadhaar")]
    [InlineData("Artisans", "artisans.js", "arMobile", "arAadhaar")]
    [InlineData("Applications", "applications.js", "apMobile", "apAadhaar")]
    public void Every_form_uses_the_shared_rules(string page, string script, string mobileId, string aadhaarId)
    {
        var view = AdminFile("Views", page, "Index.cshtml");
        Assert.Matches($"<input[^>]*id=\"{mobileId}\"[^>]*data-format=\"mobile\"|<input[^>]*data-format=\"mobile\"[^>]*id=\"{mobileId}\"", view);
        Assert.Matches($"<input[^>]*id=\"{aadhaarId}\"[^>]*data-format=\"aadhaar\"", view);

        var js = AdminFile("wwwroot", "js", script);
        Assert.Contains("IMSBValidate.mobile(", js);
        Assert.Contains("IMSBValidate.email(", js);
        Assert.Contains("IMSBValidate.aadhaar(", js);
    }

    // ── Staff mobile, which has records from the older 9-12 digit rule ──
    [Theory]
    [InlineData("987654321")]
    [InlineData("987654321012")]
    [InlineData("+919876543210")]
    public async Task New_staff_need_a_10_digit_mobile(string mobile)
    {
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.Mobile = mobile;

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal("Enter a valid 10-digit mobile number", errors["mobile"]);
        Assert.Empty(ctx.Staff);
    }

    [Fact]
    public async Task Older_staff_record_can_be_edited_with_its_unchanged_mobile_but_not_changed_to_another_invalid_one()
    {
        using var ctx = TestHelpers.CreateContext();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP001", "Older Record", "900000001"));
        await ctx.SaveChangesAsync();
        var dto = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(mobile: "900000001", email: null), "ON_LEAVE");
        dto.Aadhaar = null;

        Assert.IsType<OkObjectResult>(await TestHelpers.CreateController(ctx).Update("EMP001", dto));
        Assert.Equal("ON_LEAVE", (await ctx.Staff.SingleAsync()).Status);

        dto.Mobile = "900000002";
        Assert.Equal("Enter a valid 10-digit mobile number", TestHelpers.Errors(await TestHelpers.CreateController(ctx).Update("EMP001", dto))["mobile"]);
        Assert.Equal("900000001", (await ctx.Staff.AsNoTracking().SingleAsync()).Mobile);
    }
}
