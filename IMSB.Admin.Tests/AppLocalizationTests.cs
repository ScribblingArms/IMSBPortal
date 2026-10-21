using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IMSB.Admin.Tests;

/// <summary>
/// Localization coverage for every page: keys used by views, controllers, models and page scripts must exist in all
/// four cultures, Sales/Dashboard strings must really be translated, and placeholders must line up.
/// </summary>
public class AppLocalizationTests
{
    public static readonly string[] Cultures = { "en-US", "ta-IN", "te-IN", "hi-IN" };

    private static string AdminDir() => Path.GetFullPath(typeof(AppLocalizationTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!);

    private static Dictionary<string, string> Resx(string culture) =>
        XDocument.Load(Path.Combine(AdminDir(), "Resources", $"SharedResource.{culture}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { AdminDir() }.Concat(parts).ToArray()));

    private static IEnumerable<string> Views() =>
        Directory.EnumerateFiles(Path.Combine(AdminDir(), "Views"), "*.cshtml", SearchOption.AllDirectories);

    /// <summary>L["Key"] / _L["Key"] uses, the jsKeys/commonKeys lists views hand to scripts, and DataAnnotations ErrorMessage keys.</summary>
    private static HashSet<string> KeysIn(string source)
    {
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(source, @"\bL\[""(\w+)""\]")) keys.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(source, @"ErrorMessage = ""(\w+)""")) keys.Add(m.Groups[1].Value);
        foreach (Match arr in Regex.Matches(source, @"\w*Keys\s*=\s*new\[\]\s*\{([^}]*)\}", RegexOptions.Singleline))
            foreach (Match k in Regex.Matches(arr.Groups[1].Value, @"""(\w+)""")) keys.Add(k.Groups[1].Value);
        return keys;
    }

    private static HashSet<string> AllUsedKeys()
    {
        var keys = new HashSet<string>();
        foreach (var file in Views()
            .Concat(Directory.EnumerateFiles(Path.Combine(AdminDir(), "Controllers"), "*.cs"))
            .Concat(Directory.EnumerateFiles(Path.Combine(AdminDir(), "..", "IMSB.Dto", "Models"), "*.cs")))
            keys.UnionWith(KeysIn(File.ReadAllText(file)));
        return keys;
    }

    private static HashSet<string> SalesDashboardKeys()
    {
        var keys = new HashSet<string>();
        foreach (var src in new[]
        {
            Read("Views", "Sales", "Index.cshtml"), Read("Views", "Dashboard", "Index.cshtml"),
            Read("Controllers", "SalesController.cs"), Read("Controllers", "DashboardController.cs"), Read("..", "IMSB.Dto", "Models", "SalesModels.cs")
        }) keys.UnionWith(KeysIn(src));
        return keys;
    }

    [Fact]
    public void Key_scan_finds_the_page_strings()
    {
        var keys = AllUsedKeys();
        Assert.True(keys.Count > 500, $"only {keys.Count} keys found");
        Assert.Contains("SubmitTodaysSales", keys);          // view
        Assert.Contains("ValSalesItemsRequired", keys);      // controller
        Assert.Contains("ValTime", keys);                    // DTO annotation
        Assert.Contains("ActSaleDesc", keys);                // dashboard.js key list
        Assert.Contains("AttendanceSavedMsg", keys);         // _Layout common key list
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Every_used_key_exists_in_every_culture(string culture)
    {
        var resx = Resx(culture);
        var missing = AllUsedKeys().Where(k => !resx.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"{culture} missing: {string.Join(", ", missing)}");
    }

    [Fact]
    public void All_cultures_have_the_same_keys()
    {
        var en = Resx("en-US").Keys.ToHashSet();
        foreach (var culture in Cultures.Skip(1))
        {
            var other = Resx(culture).Keys.ToHashSet();
            Assert.True(en.SetEquals(other), $"{culture}: only in en-US: {string.Join(", ", en.Except(other))}; only in {culture}: {string.Join(", ", other.Except(en))}");
        }
    }

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Placeholders_match_english(string culture)
    {
        // a translation that drops or renumbers {0} would show the wrong number (or throw in string.Format)
        static string Holes(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x));
        var en = Resx("en-US");
        var other = Resx(culture);
        var wrong = en.Keys.Where(k => other.ContainsKey(k) && Holes(en[k]) != Holes(other[k])).ToList();
        Assert.True(wrong.Count == 0, $"{culture} placeholder mismatch: {string.Join(", ", wrong.Select(k => $"{k}='{other[k]}'"))}");
    }

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Sales_and_dashboard_keys_are_translated(string culture)
    {
        var en = Resx("en-US");
        var other = Resx(culture);
        var same = SalesDashboardKeys().Where(k => other[k] == en[k] && Regex.IsMatch(en[k], "[A-Za-z]{3}")).ToList();
        Assert.True(same.Count == 0, $"{culture} untranslated: {string.Join(", ", same)}");
    }

    [Theory]
    [InlineData("ta-IN", @"\p{IsTamil}")]
    [InlineData("te-IN", @"\p{IsTelugu}")]
    [InlineData("hi-IN", @"\p{IsDevanagari}")]
    public void Sales_and_dashboard_translations_use_the_right_script(string culture, string script)
    {
        var other = Resx(culture);
        var foreign = SalesDashboardKeys()
            .Where(k => Regex.IsMatch(other[k], @"[\p{IsThai}\p{IsCyrillic}]") || !Regex.IsMatch(other[k], script + "|^[\\W\\d{}A-Z-]*$"))
            .ToList();
        Assert.True(foreign.Count == 0, $"{culture} wrong script: {string.Join(", ", foreign.Select(k => $"{k}='{other[k]}'"))}");
    }

    [Fact]
    public void Tamil_sales_stock_terms_use_irruppu_not_share()
    {
        var en = Resx("en-US");
        var ta = Resx("ta-IN");
        var bad = SalesDashboardKeys().Where(k => en[k].Contains("stock", StringComparison.OrdinalIgnoreCase) && ta[k].Contains("பங்கு")).ToList();
        Assert.True(bad.Count == 0, $"'பங்கு' (share) used for stock: {string.Join(", ", bad)}");
    }

    [Fact]
    public void Corrected_translations_stay_fixed()
    {
        Assert.Equal("పెండింగ్‌లో ఉంది", Resx("te-IN")["PendingLabel"]);   // was "inside"
        Assert.Equal("நிலுவையில்", Resx("ta-IN")["PendingLabel"]);
    }

    // ── Page scripts get every string they ask for ────────────────

    private static HashSet<string> ArrayKeys(string source, string variable)
    {
        var m = Regex.Match(source, variable + @"\s*=\s*new\[\]\s*\{(.*?)\};", RegexOptions.Singleline);
        Assert.True(m.Success, $"{variable} not found");
        return Regex.Matches(m.Groups[1].Value, @"""(\w+)""").Select(x => x.Groups[1].Value).ToHashSet();
    }

    private static HashSet<string> ScriptKeys(string script, string functions) =>
        Regex.Matches(script, $@"\b(?:{functions})\('(\w+)'").Select(m => m.Groups[1].Value).ToHashSet();

    [Theory]
    [InlineData("Sales", "sales.js")]
    [InlineData("Dashboard", "dashboard.js")]
    [InlineData("Inventory", "inventory.js")]
    [InlineData("Stalls", "stalls.js")]
    [InlineData("Artisans", "artisans.js")]
    [InlineData("Applications", "applications.js")]
    [InlineData("Reports", "reports.js")]
    [InlineData("Attendance", "attendance.js")]
    public void Page_scripts_only_use_strings_their_view_provides(string page, string script)
    {
        var provided = ArrayKeys(Read("Views", page, "Index.cshtml"), "jsKeys");
        var used = ScriptKeys(Read("wwwroot", "js", script), "t");
        Assert.True(used.Count > 10, $"only {used.Count} t() calls found in {script}");
        var missing = used.Except(provided).ToList();
        Assert.True(missing.Count == 0, $"{script} uses keys not in {page} jsKeys: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Keys a script reaches through lookup tables (e.g. STATUS = { PENDING: ['PendingLabel', ...] }) instead of t('...'):
    /// any quoted word in the script that is a resource key must be handed to it by its view.
    /// </summary>
    [Theory]
    [InlineData("Views/Applications/Index.cshtml", "jsKeys", "applications.js")]
    [InlineData("Views/Reports/Index.cshtml", "jsKeys", "reports.js")]
    [InlineData("Views/Shared/_Header.cshtml", "headerKeys", "header.js")]
    [InlineData("Views/Attendance/Index.cshtml", "jsKeys", "attendance.js")]
    public void Keys_in_script_lookup_tables_are_provided(string view, string variable, string script)
    {
        var provided = ArrayKeys(Read(view.Split('/')), variable);
        var resx = Resx("en-US");
        var quoted = Regex.Matches(Read("wwwroot", "js", script), @"'([A-Z]\w+)'").Select(m => m.Groups[1].Value)
            .Where(resx.ContainsKey).ToHashSet();
        Assert.True(quoted.Count > 5, $"only {quoted.Count} keys found in {script}");
        var missing = quoted.Except(provided).ToList();
        Assert.True(missing.Count == 0, $"{script} uses keys not in {variable}: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Applications_reports_and_header_strings_are_translated(string culture)
    {
        var keys = new HashSet<string>();
        foreach (var src in new[] { Read("Views", "Applications", "Index.cshtml"), Read("Views", "Reports", "Index.cshtml"), Read("Views", "Reports", "Print.cshtml"),
                     Read("Views", "Shared", "_Header.cshtml"), Read("Controllers", "ApplicationsController.cs"), Read("Controllers", "ReportsController.cs") })
            keys.UnionWith(KeysIn(src));
        var en = Resx("en-US");
        var other = Resx(culture);
        // words that read the same in every language (PDF, CSV, ID...) are allowed
        var same = keys.Where(k => other[k] == en[k] && Regex.IsMatch(en[k], "[a-z]{3}")).OrderBy(k => k).ToList();
        Assert.True(same.Count == 0, $"{culture} untranslated: {string.Join(", ", same)}");
    }

    [Fact]
    public void Shared_scripts_only_use_common_strings_from_the_layout()
    {
        var provided = ArrayKeys(Read("Views", "Shared", "_Layout.cshtml"), "commonKeys");
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(AdminDir(), "wwwroot", "js"), "*.js"))
            missing.AddRange(ScriptKeys(File.ReadAllText(file), "IMSBT|AT|T").Except(provided).Select(k => $"{Path.GetFileName(file)}:{k}"));
        Assert.True(missing.Count == 0, string.Join(", ", missing));
    }

    [Theory]
    [InlineData("ta-IN", @"\p{IsTamil}")]
    [InlineData("te-IN", @"\p{IsTelugu}")]
    [InlineData("hi-IN", @"\p{IsDevanagari}")]
    public void Attendance_strings_are_translated_in_the_right_script(string culture, string script)
    {
        var keys = new HashSet<string>();
        foreach (var src in new[] { Read("Views", "Attendance", "Index.cshtml"), Read("Controllers", "AttendanceController.cs"), Read("..", "IMSB.Dto", "Models", "AttendanceModels.cs") })
            keys.UnionWith(KeysIn(src));
        Assert.True(keys.Count > 60, $"only {keys.Count} attendance keys found");
        var en = Resx("en-US");
        var other = Resx(culture);
        // words that read the same in every language (CSV, HH:mm...) are allowed
        var same = keys.Where(k => other[k] == en[k] && Regex.IsMatch(en[k], "[a-z]{3}")).OrderBy(k => k).ToList();
        Assert.True(same.Count == 0, $"{culture} untranslated: {string.Join(", ", same)}");
        var foreign = keys.Where(k => !Regex.IsMatch(other[k], script + "|^[\\W\\d{}A-Z-]*$")).OrderBy(k => k).ToList();
        Assert.True(foreign.Count == 0, $"{culture} wrong script: {string.Join(", ", foreign.Select(k => $"{k}='{other[k]}'"))}");
    }

    [Fact]
    public void Corrected_attendance_terms_stay_fixed()
    {
        Assert.Equal("மிகைநேரம்", Resx("ta-IN")["OT"]);      // was "ஓடி" (run)
        Assert.Equal("ఓటీ", Resx("te-IN")["OT"]);            // was "ఓటీవీ"
        Assert.Equal("అవుట్-టైమ్", Resx("te-IN")["OutTime"]); // was misspelt "ఆవుట్"
    }

    // ── Views ─────────────────────────────────────────────────────

    [Fact]
    public void No_view_sets_an_english_title()
    {
        var literal = Views().Where(v => Regex.IsMatch(File.ReadAllText(v), @"ViewData\[""Title""\]\s*=\s*""")).Select(Path.GetFileName).ToList();
        Assert.True(literal.Count == 0, $"hard-coded titles: {string.Join(", ", literal)}");
    }

    [Theory]
    [InlineData("Dashboard", "Index.cshtml")]
    [InlineData("Sales", "Index.cshtml")]
    [InlineData("Stalls", "Index.cshtml")]
    [InlineData("Reports", "Index.cshtml")]
    [InlineData("Reports", "Print.cshtml")]
    [InlineData("Shared", "_Header.cshtml")]
    [InlineData("Artisans", "Index.cshtml")]
    [InlineData("Applications", "Index.cshtml")]
    [InlineData("Attendance", "Index.cshtml")]
    [InlineData("Account", "Login.cshtml")]
    [InlineData("Account", "Forgot.cshtml")]
    [InlineData("Account", "Reset.cshtml")]
    [InlineData("Account", "AccessDenied.cshtml")]
    [InlineData("Shared", "_Footer.cshtml")]
    [InlineData("Shared", "Error.cshtml")]
    public void Visible_text_goes_through_the_localizer(string folder, string file)
    {
        var html = Read("Views", folder, file);
        // drop Razor code, scripts, styles and comments, then look for English words left between tags
        var markup = Regex.Replace(html, @"(?s)@section Scripts \{.*$|<script.*?</script>|<style.*?</style>|@\*.*?\*@|<!--.*?-->|@\{.*?\n\}", "");
        var leftovers = Regex.Matches(markup, @">\s*([A-Za-z][A-Za-z ,.'&/()–-]{3,}?)\s*<")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => !SampleData.Contains(t))
            .ToList();
        Assert.True(leftovers.Count == 0, $"{folder}/{file} has untranslated text: {string.Join(" | ", leftovers)}");
    }

    // prototype sample records still shown on pages whose modules have no database yet (names, places, categories)
    private static readonly HashSet<string> SampleData = new()
    {
        "Textiles", "Handicrafts", "Jewellery", "Pottery", "Paintings", "Food", "Herbal Products", "Food & Snacks",
        "Chennai", "Coimbatore", "Madurai", "Tiruchirappalli", "Salem", "Sales", "Admin", "Security", "Housekeeping", "Accounts", "IT Support",
        "Meena Selvam", "Radha Krishnan", "Closure", "Category Change", "Kamala Rajendran", "April 2025", "March 2025", "February 2025",
        "Block A", "Block B", "Block C", "Block D", "English"
    };
}
