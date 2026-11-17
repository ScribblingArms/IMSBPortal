using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IMSB.Admin.Tests;

/// <summary>
/// The public website (IMSB) is in English, Telugu and Hindi. Its keys are the English text itself
/// (Resources/SharedResource.te.resx / .hi.resx): every text the views, controllers, models and scripts use must be
/// translated in both languages, in the right script, with the same placeholders and markup as the English.
/// </summary>
public class WebsiteLocalizationTests
{
    public static readonly TheoryData<string, string> Languages = new()
    {
        { "te", @"\p{IsTelugu}" },
        { "hi", @"\p{IsDevanagari}" },
    };

    private static string WebsiteDir() => Path.GetFullPath(Path.Combine(typeof(WebsiteLocalizationTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AdminProjectDir").Value!, "..", "IMSB"));

    private static Dictionary<string, string> Resx(string language) =>
        XDocument.Load(Path.Combine(WebsiteDir(), "Resources", $"SharedResource.{language}.resx")).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    /// <summary>L["..."] / H["..."] / localizer["..."] uses, DataAnnotations ErrorMessage texts and const message strings.</summary>
    private static Dictionary<string, string> UsedKeys()
    {
        var keys = new Dictionary<string, string>();
        var sources = Directory.EnumerateFiles(WebsiteDir(), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml"))
            .Where(f => !Regex.IsMatch(f, @"[\\/](bin|obj)[\\/]"));
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in new[]
            {
                @"\b(?:L|H|localizer|T)\[\s*""((?:[^""\\]|\\.)+)""",
                @"ErrorMessage = ""((?:[^""\\]|\\.)+)""",
                @"const string \w+ = ""((?:[^""\\]|\\.)+)"""
            })
                foreach (Match m in Regex.Matches(text, pattern))
                    keys[Regex.Unescape(m.Groups[1].Value)] = Path.GetFileName(file);
        }
        return keys;
    }

    private static string Shape(string s) =>
        string.Join(",", Regex.Matches(s, @"\{\d+\}|<[^>]+>").Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal));

    [Fact]
    public void Key_scan_finds_the_website_strings()
    {
        var keys = UsedKeys();
        Assert.True(keys.Count > 300, $"only {keys.Count} keys found");
        Assert.Contains("Apply for a Stall", keys.Keys);           // layout
        Assert.Contains("Enter the name of your SHG", keys.Keys);  // model annotation
        Assert.Contains("Request ID:", keys.Keys);                 // error page
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_used_text_is_translated(string language, string script)
    {
        _ = script;
        var resx = Resx(language);
        var missing = UsedKeys().Where(k => !resx.TryGetValue(k.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(k => $"[{k.Value}] {k.Key}").OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"{language} missing: {string.Join(" | ", missing)}");
    }

    [Fact]
    public void Telugu_and_Hindi_have_the_same_keys()
    {
        var te = Resx("te").Keys.ToHashSet();
        var hi = Resx("hi").Keys.ToHashSet();
        Assert.True(te.SetEquals(hi), $"only in te: {string.Join(" | ", te.Except(hi))}; only in hi: {string.Join(" | ", hi.Except(te))}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Placeholders_and_markup_match_english(string language, string script)
    {
        _ = script;
        // a translation that drops {0} or a link would show the wrong value (or throw in string.Format)
        var wrong = Resx(language).Where(kv => Shape(kv.Key) != Shape(kv.Value)).Select(kv => $"{kv.Key} => {kv.Value}").ToList();
        Assert.True(wrong.Count == 0, $"{language} mismatch: {string.Join(" | ", wrong)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Translations_use_the_right_script(string language, string script)
    {
        // codes and names that read the same in every language (VIJAYA DAIRY is translated, SHG ID / PIN are not words)
        var wrong = Resx(language)
            .Where(kv => Regex.IsMatch(kv.Key, "[a-z]{3}") && !Regex.IsMatch(kv.Value, script))
            .Select(kv => $"{kv.Key} => {kv.Value}").ToList();
        Assert.True(wrong.Count == 0, $"{language} not in its script: {string.Join(" | ", wrong)}");
    }

    [Fact]
    public void Hindi_is_a_supported_website_language()
    {
        var program = File.ReadAllText(Path.Combine(WebsiteDir(), "Program.cs"));
        Assert.Contains("new CultureInfo(\"hi-IN\")", program);
        Assert.Contains("UnicodeRanges.Devanagari", program);
        var layout = File.ReadAllText(Path.Combine(WebsiteDir(), "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("asp-route-culture=\"hi\"", layout);
    }
}
