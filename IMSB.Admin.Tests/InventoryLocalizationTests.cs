using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IMSB.Admin.Tests;

public class InventoryLocalizationTests
{
    private static string AdminDir() => Path.GetFullPath(typeof(InventoryLocalizationTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!);

    private static Dictionary<string, string> Resx(string culture) =>
        XDocument.Load(Path.Combine(AdminDir(), "Resources", $"SharedResource.{culture}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    /// <summary>Every key used by the Inventory view, inventory.js strings, InventoryController and the DTO annotations.</summary>
    private static HashSet<string> InventoryKeys()
    {
        var root = AdminDir();
        var view = File.ReadAllText(Path.Combine(root, "Views", "Inventory", "Index.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers", "InventoryController.cs"));
        var dtos = File.ReadAllText(Path.Combine(root, "..", "IMSB.Dto", "Models", "InventoryModels.cs"));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(view, @"L\[""(\w+)""\]")) keys.Add(m.Groups[1].Value);
        var js = Regex.Match(view, @"var jsKeys = new\[\] \{(.*?)\};", RegexOptions.Singleline).Groups[1].Value;
        foreach (Match k in Regex.Matches(js, @"""(\w+)""")) keys.Add(k.Groups[1].Value);
        foreach (Match m in Regex.Matches(controller, @"_L\[""(\w+)""\]")) keys.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(dtos, @"ErrorMessage = ""(\w+)""")) keys.Add(m.Groups[1].Value);
        return keys;
    }

    [Fact]
    public void Inventory_key_scan_finds_the_page_strings()
    {
        var keys = InventoryKeys();
        Assert.True(keys.Count > 80, $"only {keys.Count} keys found");
        Assert.Contains("StockLevelByCategory", keys);
        Assert.Contains("ValInsufficientStock", keys);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Every_inventory_key_exists(string culture)
    {
        var resx = Resx(culture);
        var missing = InventoryKeys().Where(k => !resx.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        Assert.True(missing.Count == 0, $"{culture} missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Inventory_keys_are_translated(string culture)
    {
        var en = Resx("en-US");
        var other = Resx(culture);
        var same = InventoryKeys().Where(k => other[k] == en[k] && Regex.IsMatch(en[k], "[A-Za-z]{3}")).ToList();
        Assert.True(same.Count == 0, $"{culture} untranslated: {string.Join(", ", same)}");
    }

    [Theory]
    [InlineData("ta-IN", @"\p{IsTamil}")]
    [InlineData("te-IN", @"\p{IsTelugu}")]
    [InlineData("hi-IN", @"\p{IsDevanagari}")]
    public void Inventory_translations_use_the_right_script(string culture, string script)
    {
        // regression: te-IN "Quantity" was Thai and ta-IN "StockLevelByCategory" contained Cyrillic
        var other = Resx(culture);
        var foreign = InventoryKeys()
            .Where(k => Regex.IsMatch(other[k], @"[\p{IsThai}\p{IsCyrillic}]") || !Regex.IsMatch(other[k], script + "|^[\\W\\d{}A-Z-]*$"))
            .ToList();
        Assert.True(foreign.Count == 0, $"{culture} wrong script: {string.Join(", ", foreign.Select(k => $"{k}='{other[k]}'"))}");
    }

    [Fact]
    public void Tamil_stock_terms_use_irruppu_not_share()
    {
        var ta = Resx("ta-IN");
        foreach (var key in new[] { "InStock", "LowStock", "StockUpdate", "MinStock", "UpdateStock" })
            Assert.DoesNotContain("பங்கு", ta[key]);   // "share", not "stock"
        Assert.DoesNotContain("ஊதியம்", ta["BelowMinStock"]);   // "salary"
    }
}
