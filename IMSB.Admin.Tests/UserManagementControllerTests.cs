using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>User Management: District Officer and State Admin Officer logins saved in the logins table.</summary>
public class UserManagementControllerTests
{
    private static UserManagementController Controller(IMSBContext context) =>
        new(new IMSB.Backend.UserManagement(context), TestHelpers.LocalizerFactory())
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

    /// <summary>A valid form; the email defaults to {userName}@example.com so every user gets its own.</summary>
    private static UserFormDto Form(string userName = "ravi.kumar", string? password = "Secret@123", string? confirm = null,
        string mobile = "9876543210", string userType = UserTypes.DistrictOfficer, string district = "Hyderabad", string? email = null) => new()
    {
        UserName = userName, Email = email ?? userName + "@example.com", Password = password, ConfirmPassword = confirm ?? password,
        Mobile = mobile, UserType = userType, District = district
    };

    private static bool PasswordIs(Login login, string password) =>
        new PasswordHasher<Login>().VerifyHashedPassword(login, login.PasswordHash, password) != PasswordVerificationResult.Failed;

    private static async Task<Login> CreateUser(IMSBContext context, UserFormDto form)
    {
        Assert.IsType<OkObjectResult>(await Controller(context).Create(form));
        return await context.Logins.AsNoTracking().SingleAsync(l => l.Username == form.UserName);
    }

    // ── Save ──────────────────────────────────────────────────────
    [Fact]
    public async Task Create_saves_a_district_officer_in_the_logins_table_with_a_hashed_password()
    {
        using var context = TestHelpers.CreateContext();

        var result = TestHelpers.OkJson(await Controller(context).Create(Form()));

        Assert.Equal("User saved.", result.GetProperty("message").GetString());
        var saved = await context.Logins.SingleAsync();
        Assert.Equal("ravi.kumar", saved.Username);
        Assert.Equal("ravi.kumar@example.com", saved.Email);
        Assert.Equal("DistrictOfficer", saved.Role);
        Assert.Equal("9876543210", saved.Mobile);
        Assert.Equal("Hyderabad", saved.District);
        Assert.True(saved.IsActive);
        Assert.NotEqual("Secret@123", saved.PasswordHash);
        Assert.True(PasswordIs(saved, "Secret@123"));
    }

    [Fact]
    public async Task Create_stores_the_district_as_listed_whatever_its_case()
    {
        using var context = TestHelpers.CreateContext();

        var saved = await CreateUser(context, Form(district: "medchal-MALKAJGIRI"));

        Assert.Equal("Medchal-Malkajgiri", saved.District);
    }

    [Theory]
    [InlineData("user_name", "ab", null, null, null, null, "Username must be 3-100 characters: letters, digits, . _ @ -")]
    [InlineData("user_name", "has space", null, null, null, null, "Username must be 3-100 characters: letters, digits, . _ @ -")]
    [InlineData("password", null, "", "", null, null, "Password is required")]
    [InlineData("password", null, "short", "short", null, null, "Password must be 8-100 characters")]
    [InlineData("confirm_password", null, "Secret@123", "Secret@124", null, null, "Passwords do not match")]
    [InlineData("mobile", null, null, null, "12345", null, "Enter a valid 10-digit mobile number")]
    [InlineData("mobile", null, null, null, "5876543210", null, "Enter a valid 10-digit mobile number")]
    [InlineData("district", null, null, null, null, "Chennai", "Select a district")]
    [InlineData("district", null, null, null, null, "", "Select a district")]
    public async Task Create_rejects_an_invalid_form(string field, string? userName, string? password, string? confirm,
        string? mobile, string? district, string message)
    {
        using var context = TestHelpers.CreateContext();
        var form = Form(userName ?? "ravi.kumar", password ?? "Secret@123", confirm ?? password ?? "Secret@123",
            mobile ?? "9876543210", district: district ?? "Hyderabad");

        var errors = TestHelpers.Errors(await Controller(context).Create(form));

        Assert.Equal(message, errors[field]);
        Assert.Empty(context.Logins);
    }

    // ── Email ─────────────────────────────────────────────────────
    [Theory]
    [InlineData("", "Email is required")]
    [InlineData("   ", "Email is required")]
    [InlineData("ravi", "Invalid email")]
    [InlineData("ravi@site", "Invalid email")]
    [InlineData("ravi@@site.in", "Invalid email")]
    [InlineData("ravi kumar@site.in", "Invalid email")]
    public async Task Create_rejects_a_missing_or_invalid_email(string email, string message)
    {
        using var context = TestHelpers.CreateContext();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(email: email)));

        Assert.Equal(message, errors["email"]);
        Assert.Empty(context.Logins);
    }

    [Fact]
    public async Task Create_rejects_an_email_longer_than_200_characters()
    {
        using var context = TestHelpers.CreateContext();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(email: new string('a', 190) + "@example.com")));

        Assert.Equal("Invalid email", errors["email"]);
    }

    [Fact]
    public async Task Create_saves_the_email_trimmed()
    {
        using var context = TestHelpers.CreateContext();

        var saved = await CreateUser(context, Form(email: "  Ravi.Kumar@Example.in "));

        Assert.Equal("Ravi.Kumar@Example.in", saved.Email);
    }

    [Fact]
    public async Task Create_refuses_an_email_already_used_by_any_login_ignoring_case()
    {
        using var context = TestHelpers.CreateContext();
        context.Logins.Add(new Login { Username = "admin", Email = "admin@imsb.in", PasswordHash = "x", Role = "Admin" });
        await context.SaveChangesAsync();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(email: "ADMIN@imsb.in")));

        Assert.Equal("Email already registered", errors["email"]);
        Assert.Single(context.Logins);
    }

    [Fact]
    public async Task Update_refuses_the_email_of_another_login_but_keeps_its_own()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());
        await CreateUser(context, Form(userName: "sita", mobile: "9000000003"));

        var errors = TestHelpers.Errors(await Controller(context).Update(user.Id, Form(password: "", email: "SITA@example.com")));
        Assert.Equal("Email already registered", errors["email"]);

        Assert.IsType<OkObjectResult>(await Controller(context).Update(user.Id, Form(password: "", email: "Ravi.Kumar@Example.com")));
        Assert.Equal("Ravi.Kumar@Example.com", (await context.Logins.AsNoTracking().SingleAsync(l => l.Id == user.Id)).Email);
    }

    // ── Password and confirm password ─────────────────────────────
    [Theory]
    [InlineData("secret@123", "Password must have an upper-case letter, a lower-case letter, a number and a special character, with no spaces")]
    [InlineData("SECRET@123", "Password must have an upper-case letter, a lower-case letter, a number and a special character, with no spaces")]
    [InlineData("Secret@abc", "Password must have an upper-case letter, a lower-case letter, a number and a special character, with no spaces")]
    [InlineData("Secret1234", "Password must have an upper-case letter, a lower-case letter, a number and a special character, with no spaces")]
    [InlineData("Secret @123", "Password must have an upper-case letter, a lower-case letter, a number and a special character, with no spaces")]
    [InlineData("Se@1", "Password must be 8-100 characters")]
    public async Task Create_rejects_a_weak_password(string password, string message)
    {
        using var context = TestHelpers.CreateContext();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(password: password)));

        Assert.Equal(message, errors["password"]);
        Assert.Empty(context.Logins);
    }

    [Fact]
    public async Task Create_needs_the_password_confirmed()
    {
        using var context = TestHelpers.CreateContext();
        var form = Form();
        form.ConfirmPassword = "";

        var errors = TestHelpers.Errors(await Controller(context).Create(form));

        Assert.Equal("Confirm the password", errors["confirm_password"]);
        Assert.Empty(context.Logins);
    }

    [Fact]
    public async Task Update_with_only_a_confirm_password_asks_for_the_password()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());
        var form = Form(password: "");
        form.ConfirmPassword = "Secret@999";

        var errors = TestHelpers.Errors(await Controller(context).Update(user.Id, form));

        Assert.Equal("Password is required", errors["password"]);
        Assert.True(PasswordIs(await context.Logins.AsNoTracking().SingleAsync(), "Secret@123"));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Staff")]
    [InlineData("WebsiteUser")]
    [InlineData("districtofficer")]
    [InlineData("")]
    public async Task Create_accepts_only_the_two_officer_types(string userType)
    {
        using var context = TestHelpers.CreateContext();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(userType: userType)));

        Assert.Equal("Select a user type", errors["user_type"]);
        Assert.Empty(context.Logins);
    }

    [Fact]
    public async Task Create_refuses_a_username_already_used_by_any_login_ignoring_case()
    {
        using var context = TestHelpers.CreateContext();
        context.Logins.Add(new Login { Username = "admin", PasswordHash = "x", Role = "Admin" });
        await context.SaveChangesAsync();

        var errors = TestHelpers.Errors(await Controller(context).Create(Form(userName: "ADMIN")));

        Assert.Equal("This username is already in use", errors["user_name"]);
        Assert.Single(context.Logins);
    }

    [Fact]
    public async Task Create_without_a_body_is_rejected()
    {
        using var context = TestHelpers.CreateContext();
        Assert.IsType<BadRequestObjectResult>(await Controller(context).Create(null));
    }

    // ── View ──────────────────────────────────────────────────────
    [Fact]
    public async Task Data_lists_only_officers_without_passwords()
    {
        using var context = TestHelpers.CreateContext();
        context.Logins.Add(new Login { Username = "admin", PasswordHash = "x", Role = "Admin" });
        await context.SaveChangesAsync();
        await CreateUser(context, Form(userName: "zara", mobile: "9000000002", district: "Warangal"));
        await CreateUser(context, Form(userName: "anil", mobile: "9000000001", district: "Medchal-Malkajgiri"));

        var data = TestHelpers.OkJson(await Controller(context).Data());

        var rows = data.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(new[] { "anil", "zara" }, rows.Select(r => r.GetProperty("userName").GetString()));
        var anil = rows[0];
        Assert.Equal("anil@example.com", anil.GetProperty("email").GetString());
        Assert.Equal("9000000001", anil.GetProperty("mobile").GetString());
        Assert.Equal("DistrictOfficer", anil.GetProperty("userType").GetString());
        Assert.Equal("District Officer", anil.GetProperty("userTypeLabel").GetString());
        Assert.Equal("Medchal-Malkajgiri", anil.GetProperty("district").GetString());
        Assert.Equal("Medchal-Malkajgiri", anil.GetProperty("districtLabel").GetString());
        Assert.False(anil.TryGetProperty("passwordHash", out _));
        Assert.False(anil.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Data_offers_the_33_telangana_districts_and_the_two_officer_types()
    {
        using var context = TestHelpers.CreateContext();

        var data = TestHelpers.OkJson(await Controller(context).Data());

        var districts = data.GetProperty("districts").EnumerateArray().ToList();
        Assert.Equal(33, districts.Count);
        Assert.Contains(districts, d => d.GetProperty("value").GetString() == "Hyderabad" && d.GetProperty("label").GetString() == "Hyderabad");
        var types = data.GetProperty("userTypes").EnumerateArray()
            .Select(t => (t.GetProperty("value").GetString(), t.GetProperty("label").GetString())).ToList();
        Assert.Equal(new (string?, string?)[] { ("DistrictOfficer", "District Officer"), ("StateAdminOfficer", "State Admin Officer") }, types);
    }

    [Fact]
    public async Task Every_district_has_a_label_in_every_culture()
    {
        using var context = TestHelpers.CreateContext();
        foreach (var culture in AppLocalizationTests.Cultures)
        {
            using var _ = new TestHelpers.CultureScope(culture);
            var districts = TestHelpers.OkJson(await Controller(context).Data()).GetProperty("districts").EnumerateArray().ToList();
            Assert.All(districts, d => Assert.DoesNotContain("District_", d.GetProperty("label").GetString()));
        }
    }

    [Theory]
    [InlineData("te-IN", "హైదరాబాద్", "జిల్లా అధికారి", "వినియోగదారు సేవ్ చేయబడ్డారు.")]
    [InlineData("hi-IN", "हैदराबाद", "जिला अधिकारी", "उपयोगकर्ता सहेजा गया।")]
    [InlineData("ta-IN", "ஹைதராபாத்", "மாவட்ட அலுவலர்", "பயனர் சேமிக்கப்பட்டார்.")]
    public async Task Labels_and_messages_follow_the_ui_culture(string culture, string hyderabad, string districtOfficer, string saved)
    {
        using var context = TestHelpers.CreateContext();
        using var _ = new TestHelpers.CultureScope(culture);

        Assert.Equal(saved, TestHelpers.OkJson(await Controller(context).Create(Form())).GetProperty("message").GetString());
        var row = Assert.Single(TestHelpers.OkJson(await Controller(context).Data()).GetProperty("rows").EnumerateArray());

        Assert.Equal(hyderabad, row.GetProperty("districtLabel").GetString());
        Assert.Equal(districtOfficer, row.GetProperty("userTypeLabel").GetString());
        Assert.Equal("Hyderabad", row.GetProperty("district").GetString());   // stored in English
    }

    // ── Update ────────────────────────────────────────────────────
    [Fact]
    public async Task Update_changes_the_details_and_an_empty_password_keeps_the_current_one()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());

        var result = TestHelpers.OkJson(await Controller(context).Update(user.Id,
            Form(userName: "ravi.k", password: "", mobile: "9123456780", district: "Warangal")));

        Assert.Equal("User updated.", result.GetProperty("message").GetString());
        var saved = await context.Logins.AsNoTracking().SingleAsync();
        Assert.Equal(("ravi.k", "9123456780", "Warangal"), (saved.Username, saved.Mobile, saved.District));
        Assert.True(PasswordIs(saved, "Secret@123"));
    }

    [Fact]
    public async Task Update_with_a_new_password_replaces_it()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());

        Assert.IsType<OkObjectResult>(await Controller(context).Update(user.Id, Form(password: "NewSecret#9")));

        var saved = await context.Logins.AsNoTracking().SingleAsync();
        Assert.True(PasswordIs(saved, "NewSecret#9"));
        Assert.False(PasswordIs(saved, "Secret@123"));
    }

    [Fact]
    public async Task Update_refuses_a_password_that_does_not_match_its_confirmation()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());

        var errors = TestHelpers.Errors(await Controller(context).Update(user.Id, Form(password: "NewSecret#9", confirm: "Other#999")));

        Assert.Equal("Passwords do not match", errors["confirm_password"]);
        Assert.True(PasswordIs(await context.Logins.AsNoTracking().SingleAsync(), "Secret@123"));
    }

    [Fact]
    public async Task Update_refuses_the_username_of_another_login_but_keeps_its_own()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());
        await CreateUser(context, Form(userName: "sita", mobile: "9000000003"));

        var errors = TestHelpers.Errors(await Controller(context).Update(user.Id, Form(userName: "Sita", password: "")));
        Assert.Equal("This username is already in use", errors["user_name"]);

        Assert.IsType<OkObjectResult>(await Controller(context).Update(user.Id, Form(userName: "RAVI.KUMAR", password: "")));
        Assert.Equal("RAVI.KUMAR", (await context.Logins.AsNoTracking().SingleAsync(l => l.Id == user.Id)).Username);
    }

    [Fact]
    public async Task Update_cannot_change_an_admin_login()
    {
        using var context = TestHelpers.CreateContext();
        var admin = new Login { Username = "admin", PasswordHash = "x", Role = "Admin" };
        context.Logins.Add(admin);
        await context.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await Controller(context).Update(admin.Id, Form(userName: "admin")));

        var saved = await context.Logins.AsNoTracking().SingleAsync();
        Assert.Equal(("Admin", "x"), (saved.Role, saved.PasswordHash));
    }

    [Fact]
    public async Task Update_of_an_unknown_user_is_not_found()
    {
        using var context = TestHelpers.CreateContext();
        Assert.IsType<NotFoundResult>(await Controller(context).Update(999999, Form()));
    }

    // ── Delete ────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_removes_the_website_user()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());

        var result = TestHelpers.OkJson(await Controller(context).Delete(user.Id));

        Assert.Equal("User deleted.", result.GetProperty("message").GetString());
        Assert.Empty(context.Logins);
    }

    [Fact]
    public async Task Delete_cannot_remove_an_admin_login()
    {
        using var context = TestHelpers.CreateContext();
        var admin = new Login { Username = "admin", PasswordHash = "x", Role = "Admin" };
        context.Logins.Add(admin);
        await context.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await Controller(context).Delete(admin.Id));
        Assert.Single(context.Logins);
    }

    [Fact]
    public async Task Create_saves_a_state_admin_officer()
    {
        using var context = TestHelpers.CreateContext();

        var saved = await CreateUser(context, Form(userType: UserTypes.StateAdminOfficer));

        Assert.Equal("StateAdminOfficer", saved.Role);
    }

    [Fact]
    public async Task Update_can_change_the_user_type()
    {
        using var context = TestHelpers.CreateContext();
        var user = await CreateUser(context, Form());

        Assert.IsType<OkObjectResult>(await Controller(context).Update(user.Id, Form(password: "", userType: UserTypes.StateAdminOfficer)));

        Assert.Equal("StateAdminOfficer", (await context.Logins.AsNoTracking().SingleAsync()).Role);
    }

    // ── Sign-in ───────────────────────────────────────────────────
    [Theory]
    [InlineData("DistrictOfficer", "districtofficer")]
    [InlineData("StateAdminOfficer", "stateadminofficer")]
    [InlineData("StateAdminOfficer", "StateAdminOfficer")]
    public async Task An_officer_signs_in_with_their_own_role(string userType, string selectedRole)
    {
        using var context = TestHelpers.CreateContext();
        await CreateUser(context, Form(userType: userType));

        var (login, role, error) = await new IMSB.Backend.Account(context).CheckSignInAsync("ravi.kumar", "Secret@123", selectedRole);

        Assert.NotNull(login);
        Assert.Null(error);
        Assert.Equal(userType, role);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("staff")]
    [InlineData("inventory")]
    [InlineData("stateadminofficer")]
    public async Task A_district_officer_cannot_sign_in_as_another_role(string selectedRole)
    {
        using var context = TestHelpers.CreateContext();
        await CreateUser(context, Form());

        var (login, _, error) = await new IMSB.Backend.Account(context).CheckSignInAsync("ravi.kumar", "Secret@123", selectedRole);

        Assert.Null(login);
        Assert.Equal("RoleNotAuthorized", error);
    }

    [Theory]
    [InlineData("websiteuser")]
    [InlineData("superuser")]
    [InlineData("")]
    public async Task An_unknown_sign_in_role_is_refused(string selectedRole)
    {
        using var context = TestHelpers.CreateContext();
        await CreateUser(context, Form());

        var (_, _, error) = await new IMSB.Backend.Account(context).CheckSignInAsync("ravi.kumar", "Secret@123", selectedRole);

        Assert.Equal("InvalidRoleSelected", error);
    }

    [Fact]
    public void Stall_management_is_open_to_admin_and_both_officer_roles() =>
        Assert.Equal(new[] { "Admin", "DistrictOfficer", "StateAdminOfficer" }, UserRoles.StallsAccess.Split(','));

    [Fact]
    public void The_sign_in_dropdown_offers_the_five_roles_in_order() =>
        Assert.Equal(new[] { "admin", "staff", "inventory", "districtofficer", "stateadminofficer" }, UserRoles.SignInChoices.Select(c => c.Value));

    [Theory]
    [InlineData("DistrictOfficer", "Stalls")]
    [InlineData("StateAdminOfficer", "Stalls")]
    [InlineData("Admin", "Dashboard")]
    [InlineData("Staff", "Staff")]
    [InlineData("Inventory", "Inventory")]
    public void Each_role_lands_on_its_home_page(string role, string controller) =>
        Assert.Equal(controller, UserRoles.HomeController(role));

    // ── Form rules (UserFormDto.Validate) ─────────────────────────
    [Theory]
    [InlineData(true, "abc", "Secret@123", "Secret@123", null)]
    [InlineData(true, "user@site.in", "Secret@123", "Secret@123", null)]
    [InlineData(false, "abc", "", "", null)]                                   // update: empty keeps the password
    [InlineData(false, "abc", null, null, null)]
    [InlineData(true, "abc", null, null, "password:ValPasswordRequired")]
    [InlineData(false, "abc", "1234567", "1234567", "password:ValPasswordLength")]
    [InlineData(false, "abc", "abcdefgh", "abcdefgh", "password:ValPasswordStrength")]
    [InlineData(true, "abc", "Secret@123", "", "confirm_password:ValConfirmPasswordRequired")]
    [InlineData(true, "abc", "Secret@123", "Secret@12", "confirm_password:ValPasswordsDoNotMatch")]
    [InlineData(false, "abc", "", "Secret@123", "password:ValPasswordRequired")]
    [InlineData(true, " ", "Secret@123", "Secret@123", "user_name:ValUserName")]
    [InlineData(true, "abc#", "Secret@123", "Secret@123", "user_name:ValUserName")]
    public void Validate_applies_the_form_rules(bool isNew, string userName, string? password, string? confirm, string? expected)
    {
        var form = new UserFormDto
        {
            UserName = userName, Email = "abc@example.com", Password = password, ConfirmPassword = confirm, Mobile = "9876543210",
            UserType = UserTypes.StateAdminOfficer, District = "Nalgonda"
        };
        var errors = form.Validate(isNew);
        Assert.Equal(expected, errors.Count == 0 ? null : string.Join(",", errors.Select(e => $"{e.Key}:{e.Value}")));
    }

    [Fact]
    public void Every_district_has_a_resource_key()
    {
        Assert.Equal(33, TelanganaDistricts.All.Length);
        Assert.Equal("District_MedchalMalkajgiri", DistrictNames.ResourceKey("Medchal-Malkajgiri"));
        Assert.Equal("District_YadadriBhuvanagiri", DistrictNames.ResourceKey("Yadadri Bhuvanagiri"));
        Assert.Null(DistrictNames.ResourceKey("Chennai"));
    }
}
