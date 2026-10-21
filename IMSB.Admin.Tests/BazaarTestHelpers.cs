using System.Security.Claims;
using IMSB.Admin.Controllers;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

/// <summary>Fixtures for the stall and artisan tests.</summary>
internal static class BazaarTestHelpers
{
    public static readonly Guid Textiles = InventoryTestHelpers.Textiles;
    public static readonly Guid Pottery = InventoryTestHelpers.Pottery;

    private static ControllerContext Context(string userName) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, userName), new Claim(ClaimTypes.Role, "Admin") }, "Test"))
        }
    };

    public static StallsController Stalls(IMSBContext ctx, string user = "admin@imsb") =>
        new(new IMSB.Backend.Stalls(ctx), TestHelpers.LocalizerFactory()) { ControllerContext = Context(user) };

    public static ArtisansController Artisans(IMSBContext ctx, string user = "admin@imsb") =>
        new(new IMSB.Backend.Artisans(ctx), TestHelpers.LocalizerFactory()) { ControllerContext = Context(user) };

    /// <summary>A valid Aadhaar number (Verhoeff check digit) built from a seed.</summary>
    public static string ValidAadhaar(int seed)
    {
        var eleven = (40000000000L + seed * 7919L).ToString();
        return eleven + Aadhaar.CheckDigit(eleven);
    }

    public static Artisan AddArtisan(IMSBContext ctx, string code, string name, string mobile, string status = ArtisanStatuses.Active,
        Guid? category = null, string district = "Hyderabad", int seed = 0)
    {
        var aadhaar = ValidAadhaar(seed == 0 ? int.Parse(code[^4..]) : seed);
        var a = new Artisan
        {
            ArtisanCode = code, FullName = name, DateOfBirth = new DateTime(1985, 5, 5), Gender = "Female", Mobile = mobile,
            Address = "1, Main Road", AadhaarHash = Aadhaar.Hash(aadhaar), AadhaarLast4 = Aadhaar.Last4(aadhaar), District = district,
            CategoryId = category ?? Textiles, Status = status, JoinedDate = new DateTime(2024, 1, 15)
        };
        ctx.Artisans.Add(a);
        ctx.SaveChanges();
        return a;
    }

    public static Stall AddStall(IMSBContext ctx, string code, string block = "A", string status = StallStatuses.Vacant, Artisan? artisan = null)
    {
        var s = new Stall
        {
            StallCode = code, Name = "Stall " + code, Block = block, Status = artisan != null ? StallStatuses.Occupied : status,
            ArtisanId = artisan?.Id, CategoryId = artisan?.CategoryId, AllottedDate = artisan != null ? new DateTime(2025, 1, 1) : null
        };
        ctx.Stalls.Add(s);
        ctx.SaveChanges();
        return s;
    }

    /// <summary>
    /// Categories + stalls A-01 (Kamala), A-02 (vacant), B-01 (vacant), B-02 (reserved), C-01 (maintenance);
    /// artisans ART-0001 Kamala (A-01), ART-0002 Saroja (free), ART-0003 Meena (free, on leave), ART-0004 Lakshmi (inactive).
    /// </summary>
    public static IMSBContext Seeded()
    {
        var ctx = InventoryTestHelpers.CreateContext();
        var kamala = AddArtisan(ctx, "ART-0001", "Kamala Rajendran", "9876543210");
        AddArtisan(ctx, "ART-0002", "Saroja Murugan", "8765432109", category: Pottery, district: "Warangal");
        AddArtisan(ctx, "ART-0003", "Meena Selvam", "7654321098", ArtisanStatuses.OnLeave);
        AddArtisan(ctx, "ART-0004", "Lakshmi Velu", "6543210987", ArtisanStatuses.Inactive, district: "Warangal");
        AddStall(ctx, "A-01", "A", artisan: kamala);
        AddStall(ctx, "A-02", "A");
        AddStall(ctx, "B-01", "B");
        AddStall(ctx, "B-02", "B", StallStatuses.Reserved);
        AddStall(ctx, "C-01", "C", StallStatuses.Maintenance);
        return ctx;
    }

    public static StallDto NewStall(string code = "D-01", string name = "Stall D-01", string block = "d", string? status = null) => new()
    {
        StallCode = code, Name = name, Block = block, RowLabel = "Row 1", Status = status, MonthlyRent = 2500, Location = "Near gate"
    };

    public static AllocateStallDto Allocation(string stall = "A-02", string artisan = "ART-0002", string? date = null, string? end = null) => new()
    {
        StallCode = stall, ArtisanCode = artisan, AllottedDate = date ?? DateTime.Today.ToString("yyyy-MM-dd"), AgreementEndDate = end, MonthlyRent = 3000
    };

    public static ReallocationDto Reallocation(string stall = "A-01", string reason = "ARTISAN_REQUEST", string? newArtisan = "ART-0002") => new()
    {
        StallCode = stall, Reason = reason, NewArtisanCode = newArtisan, RequestedDate = DateTime.Today.ToString("yyyy-MM-dd"), Remarks = "moving"
    };

    public static ArtisanDto NewArtisan(string mobile = "9123456780", int aadhaarSeed = 501, string name = "Anitha Jayakumar") => new()
    {
        FullName = name, DateOfBirth = "1990-04-12", Gender = "Female", Mobile = mobile, Email = "Anitha@Example.com",
        Address = "12, Temple Street, Siddipet", Aadhaar = ValidAadhaar(aadhaarSeed), District = "Siddipet",
        CategoryId = Textiles.ToString(), YearsOfExperience = 5, Skills = "Handloom weaving"
    };

    public static ArtisanDto ToDto(Artisan a, string? status = null) => new()
    {
        FullName = a.FullName, DateOfBirth = a.DateOfBirth.ToString("yyyy-MM-dd"), Gender = a.Gender, Mobile = a.Mobile, Email = a.Email,
        Address = a.Address, District = a.District, CategoryId = a.CategoryId.ToString(), YearsOfExperience = a.YearsOfExperience,
        Skills = a.Skills, Status = status ?? a.Status, JoinedDate = a.JoinedDate.ToString("yyyy-MM-dd")
    };
}
