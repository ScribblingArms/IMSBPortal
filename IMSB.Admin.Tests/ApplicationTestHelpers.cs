using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>Fixtures for the applications, reports, search and notification tests.</summary>
internal static class ApplicationTestHelpers
{
    public static ControllerContext Context(string userName = "admin@imsb", string role = "Admin") => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, role) }, "Test"))
        }
    };

    public static ApplicationsController Applications(IMSBContext ctx, string user = "admin@imsb") =>
        new(new IMSB.Backend.Applications(ctx), TestHelpers.LocalizerFactory()) { ControllerContext = Context(user) };

    public static ReportsController Reports(IMSBContext ctx, string user = "admin@imsb") =>
        new(new IMSB.Backend.Reports(ctx), new IMSB.Backend.Attendance(ctx), new IMSB.Backend.Artisans(ctx),
            TestHelpers.LocalizerFactory()) { ControllerContext = Context(user) };

    public static SearchController Search(IMSBContext ctx, string role = "Admin") =>
        new(new IMSB.Backend.Search(ctx), TestHelpers.LocalizerFactory()) { ControllerContext = Context("someone", role) };

    public static NotificationsController Notifications(IMSBContext ctx, string role = "Admin") =>
        new(new IMSB.Backend.Notifications(ctx)) { ControllerContext = Context("someone", role) };

    /// <summary>A stored application; the Aadhaar comes from seed (default: the number in the code + 5000, clear of the artisans' numbers).</summary>
    public static ArtisanApplication AddApplication(IMSBContext ctx, string no, string status = ApplicationStatuses.Pending, Guid? category = null,
        string? mobile = null, DateTime? submitted = null, DateTime? decidedAt = null, string name = "Applicant", string district = "Hyderabad", int seed = 0)
    {
        var n = int.Parse(no[^4..]);
        var aadhaar = BazaarTestHelpers.ValidAadhaar(seed == 0 ? 5000 + n : seed);
        var a = new ArtisanApplication
        {
            ApplicationNo = no, FullName = name + " " + n, DateOfBirth = new DateTime(1990, 1, 1), Gender = "Female",
            Mobile = mobile ?? $"70000{n:D5}", Address = "5, Market Road", AadhaarHash = Aadhaar.Hash(aadhaar), AadhaarLast4 = Aadhaar.Last4(aadhaar),
            District = district, CategoryId = category ?? BazaarTestHelpers.Textiles, Status = status,
            SubmittedDate = submitted ?? DateTime.Today.AddDays(-3), DecidedAt = decidedAt
        };
        ctx.ArtisanApplications.Add(a);
        ctx.SaveChanges();
        return a;
    }

    public static ApplicationDto NewApplication(string mobile = "9123456780", int aadhaarSeed = 801, string name = "Sridevi Mallesh") => new()
    {
        FullName = name, DateOfBirth = "1992-06-15", Gender = "Female", Mobile = mobile, Email = "Sridevi@Example.com",
        Address = "7, Temple Street, Warangal", Aadhaar = BazaarTestHelpers.ValidAadhaar(aadhaarSeed), District = "Warangal",
        CategoryId = BazaarTestHelpers.Pottery.ToString(), YearsOfExperience = 6, Skills = "Terracotta"
    };

    public static ApplicationDto ToDto(ArtisanApplication a) => new()
    {
        FullName = a.FullName, DateOfBirth = a.DateOfBirth.ToString("yyyy-MM-dd"), Gender = a.Gender, Mobile = a.Mobile, Email = a.Email,
        Address = a.Address, District = a.District, CategoryId = a.CategoryId.ToString(), YearsOfExperience = a.YearsOfExperience,
        Skills = a.Skills, SubmittedDate = a.SubmittedDate.ToString("yyyy-MM-dd"), Remarks = a.Remarks
    };
}
