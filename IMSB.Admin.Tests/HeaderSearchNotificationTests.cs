using System.Text.Json;
using IMSB.Dto.Models;
using static IMSB.Admin.Tests.ApplicationTestHelpers;
using static IMSB.Admin.Tests.BazaarTestHelpers;

namespace IMSB.Admin.Tests;

public class HeaderSearchTests
{
    private static Dictionary<string, List<(string Title, string Url)>> Groups(JsonElement json) =>
        json.GetProperty("groups").EnumerateArray().ToDictionary(g => g.GetProperty("type").GetString()!,
            g => g.GetProperty("items").EnumerateArray().Select(i => (i.GetProperty("title").GetString()!, i.GetProperty("url").GetString()!)).ToList());

    private static IMSBContext Context()
    {
        var ctx = Seeded();
        ctx.Staff.Add(TestHelpers.ExistingStaff("EMP-0001", "Kamakshi Rao", "9000000001"));
        ctx.SaveChanges();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Kamal Lamp", Textiles, "A-01", 5, 1);
        AddApplication(ctx, "APP-0001", ApplicationStatuses.UnderReview, name: "Kamaladevi");
        return ctx;
    }

    [Fact]
    public async Task Admin_searches_every_module()
    {
        using var ctx = Context();
        var g = Groups(TestHelpers.OkJson(await Search(ctx).Query("kama")));
        Assert.Equal(new[] { "ARTISANS", "APPLICATIONS", "STALLS", "STAFF", "PRODUCTS" }, g.Keys);
        Assert.Equal(("Kamala Rajendran", "/Artisans/Index?q=ART-0001"), g["ARTISANS"].Single());
        Assert.Equal("/Applications/Index?tab=review&q=APP-0001", g["APPLICATIONS"].Single().Url);
        Assert.Equal("/Stalls/Index?q=A-01", g["STALLS"].Single().Url);
        Assert.Equal("/Staff/Index?q=EMP-0001", g["STAFF"].Single().Url);
        Assert.Equal("/Inventory/Index?q=PRD001", g["PRODUCTS"].Single().Url);
    }

    [Fact]
    public async Task Staff_role_only_sees_staff_and_its_pages()
    {
        using var ctx = Context();
        var g = Groups(TestHelpers.OkJson(await Search(ctx, "Staff").Query("kama")));
        Assert.Equal(new[] { "STAFF" }, g.Keys);
        var pages = Groups(TestHelpers.OkJson(await Search(ctx, "Staff").Query("Attend")));
        Assert.Equal("/Attendance/Index", pages["PAGES"].Single().Url);
        Assert.Empty(Groups(TestHelpers.OkJson(await Search(ctx, "Staff").Query("Reports"))));
    }

    [Fact]
    public async Task Inventory_role_only_sees_products()
    {
        using var ctx = Context();
        Assert.Equal(new[] { "PRODUCTS" }, Groups(TestHelpers.OkJson(await Search(ctx, "Inventory").Query("kama"))).Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" k ")]
    public async Task Short_queries_return_nothing(string? q)
    {
        using var ctx = Context();
        Assert.Empty(Groups(TestHelpers.OkJson(await Search(ctx).Query(q))));
    }

    [Fact]
    public async Task Mobile_digits_and_localized_page_names_match()
    {
        using var ctx = Context();
        Assert.Equal("Kamala Rajendran", Groups(TestHelpers.OkJson(await Search(ctx).Query("98765 43")))["ARTISANS"].Single().Title);

        using var _ = new TestHelpers.CultureScope("hi-IN");
        var reports = TestHelpers.SharedLocalizer()["ReportsAnalytics"].Value;
        Assert.Equal("/Reports/Index", Groups(TestHelpers.OkJson(await Search(ctx).Query(reports)))["PAGES"].Single().Url);
    }
}

public class HeaderNotificationTests
{
    private static List<(string Type, int Count, string Severity)> Items(Microsoft.AspNetCore.Mvc.IActionResult r) =>
        TestHelpers.OkJson(r).GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("type").GetString()!, i.GetProperty("count").GetInt32(), i.GetProperty("severity").GetString()!)).ToList();

    /// <summary>Two new applications, one in review, a pending reallocation, A-01's agreement ending soon, a renewal, stock alerts and a staff contract ending.</summary>
    private static async Task<IMSBContext> Context()
    {
        var ctx = Seeded();
        AddApplication(ctx, "APP-0001");
        AddApplication(ctx, "APP-0002");
        AddApplication(ctx, "APP-0003", ApplicationStatuses.UnderReview);
        await Stalls(ctx).RequestReallocation(Reallocation(newArtisan: "ART-0002"));
        ctx.Stalls.Single(s => s.StallCode == "A-01").AgreementEndDate = DateTime.Today.AddDays(10);
        ctx.Artisans.Single(a => a.ArtisanCode == "ART-0003").Status = ArtisanStatuses.RenewalDue;
        var staff = TestHelpers.ExistingStaff("EMP-0001", "Meena", "9000000001");
        staff.ContractEndDate = DateTime.Today.AddDays(5);
        ctx.Staff.Add(staff);
        ctx.SaveChanges();
        InventoryTestHelpers.AddProduct(ctx, "PRD001", "Out", Textiles, "A-01", 0, 5);
        InventoryTestHelpers.AddProduct(ctx, "PRD002", "Low", Textiles, "A-01", 2, 5);
        InventoryTestHelpers.AddProduct(ctx, "PRD003", "Fine", Textiles, "A-01", 9, 5);
        return ctx;
    }

    [Fact]
    public async Task Admin_gets_every_alert_most_urgent_first()
    {
        using var ctx = await Context();
        var items = Items(await Notifications(ctx).Data());
        Assert.Equal(new[] { "OUT_OF_STOCK", "APPS_PENDING", "REALLOCATIONS_PENDING", "AGREEMENTS_EXPIRING", "LOW_STOCK", "CONTRACTS_EXPIRING", "APPS_REVIEW", "ARTISAN_RENEWALS" },
            items.Select(i => i.Type));
        Assert.Equal((2, "warning"), items.Where(i => i.Type == "APPS_PENDING").Select(i => (i.Count, i.Severity)).Single());
        // nothing has expired, so that alert is left out
        Assert.DoesNotContain(items, i => i.Type == "AGREEMENTS_EXPIRED");
    }

    [Fact]
    public async Task Ids_change_with_the_count_so_changed_alerts_are_unread_again()
    {
        using var ctx = await Context();
        var json = TestHelpers.OkJson(await Notifications(ctx).Data());
        Assert.Contains(json.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetString() == "APPS_PENDING:2");
    }

    [Fact]
    public async Task Expired_agreements_are_danger()
    {
        using var ctx = await Context();
        ctx.Stalls.Single(s => s.StallCode == "A-01").AgreementEndDate = DateTime.Today.AddDays(-1);
        ctx.SaveChanges();
        Assert.Equal("danger", Items(await Notifications(ctx).Data()).Single(i => i.Type == "AGREEMENTS_EXPIRED").Severity);
    }

    [Theory]
    [InlineData("Inventory", "OUT_OF_STOCK,LOW_STOCK")]
    [InlineData("Staff", "CONTRACTS_EXPIRING")]
    public async Task Other_roles_only_get_their_modules(string role, string expected)
    {
        using var ctx = await Context();
        Assert.Equal(expected, string.Join(",", Items(await Notifications(ctx, role).Data()).Select(i => i.Type)));
    }

    [Fact]
    public async Task Quiet_database_has_no_alerts()
    {
        using var ctx = InventoryTestHelpers.CreateContext();
        var json = TestHelpers.OkJson(await Notifications(ctx).Data());
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());
        Assert.Equal(0, json.GetProperty("total").GetInt32());
    }
}
