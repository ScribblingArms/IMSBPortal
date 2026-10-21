using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IMSB.Admin.Tests;

public class StaffLocalizationTests
{
    private static readonly string[] Cultures = { "en-US", "ta-IN", "te-IN", "hi-IN" };

    private static string AdminProjectDir()
    {
        var dir = typeof(StaffLocalizationTests).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .Single(a => a.Key == "AdminProjectDir").Value!;
        var full = Path.GetFullPath(dir);
        Assert.True(Directory.Exists(full), $"IMSB.Admin sources not found at {full}");
        return full;
    }

    private static Dictionary<string, string> LoadResx(string culture)
    {
        var path = Path.Combine(AdminProjectDir(), "Resources", $"SharedResource.{culture}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
    }

    /// <summary>Every key the Staff view, staff.js, StaffController and the DTO annotations use.</summary>
    private static IEnumerable<string> StaffKeys()
    {
        var root = AdminProjectDir();
        var view = File.ReadAllText(Path.Combine(root, "Views", "Staff", "Index.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "Controllers", "StaffController.cs"));
        var dtos = File.ReadAllText(Path.Combine(root, "..", "IMSB.Dto", "Models", "StaffModels.cs"));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(view, @"L\[""(\w+)""\]")) keys.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(view, @"var jsKeys = new\[\] \{(.*?)\};", RegexOptions.Singleline))
            foreach (Match k in Regex.Matches(m.Groups[1].Value, @"""(\w+)""")) keys.Add(k.Groups[1].Value);
        foreach (Match m in Regex.Matches(controller, @"_L\[""(\w+)""\]")) keys.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(dtos, @"ErrorMessage = ""(\w+)""")) keys.Add(m.Groups[1].Value);
        return keys;
    }

    public static IEnumerable<object[]> CultureData() => Cultures.Select(c => new object[] { c });

    [Fact]
    public void Staff_page_uses_a_meaningful_number_of_keys()
    {
        // guards the regexes above against silently matching nothing
        Assert.True(StaffKeys().Count() > 60);
    }

    [Theory]
    [MemberData(nameof(CultureData))]
    public void Every_staff_key_exists_and_is_non_empty(string culture)
    {
        var resx = LoadResx(culture);
        var missing = StaffKeys().Where(k => !resx.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        Assert.True(missing.Count == 0, $"{culture} is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("ta-IN")]
    [InlineData("te-IN")]
    [InlineData("hi-IN")]
    public void Staff_keys_are_actually_translated(string culture)
    {
        var en = LoadResx("en-US");
        var other = LoadResx(culture);
        // values that are just placeholders/codes may legitimately match English
        var untranslated = StaffKeys().Where(k => other[k] == en[k] && Regex.IsMatch(en[k], "[A-Za-z]{3}")).ToList();
        Assert.True(untranslated.Count == 0, $"{culture} untranslated: {string.Join(", ", untranslated)}");
    }

    [Theory]
    [MemberData(nameof(CultureData))]
    public void All_cultures_have_the_same_key_set_without_duplicates(string culture)
    {
        var path = Path.Combine(AdminProjectDir(), "Resources", $"SharedResource.{culture}.resx");
        var names = XDocument.Load(path).Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Equal(LoadResx("en-US").Keys.OrderBy(k => k), names.OrderBy(k => k));
    }

    [Theory]
    [MemberData(nameof(CultureData))]
    public void Format_placeholders_match_english(string culture)
    {
        var en = LoadResx("en-US");
        var other = LoadResx(culture);
        foreach (var key in en.Keys)
        {
            var expected = Regex.Matches(en[key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
            var actual = Regex.Matches(other[key], @"\{\d+\}").Select(m => m.Value).OrderBy(x => x);
            Assert.True(expected.SequenceEqual(actual), $"{culture}:{key} placeholders differ");
        }
    }

    [Theory]
    [InlineData("en-US", "Staff must be at least 18 years old")]
    [InlineData("ta-IN", "பணியாளருக்கு குறைந்தது 18 வயது இருக்க வேண்டும்")]
    [InlineData("te-IN", "సిబ్బందికి కనీసం 18 సంవత్సరాలు ఉండాలి")]
    [InlineData("hi-IN", "स्टाफ की आयु कम से कम 18 वर्ष होनी चाहिए")]
    public async Task Controller_validation_messages_follow_request_culture(string culture, string expected)
    {
        using var scope = new TestHelpers.CultureScope(culture);
        using var ctx = TestHelpers.CreateContext();
        var dto = TestHelpers.ValidCreateDto();
        dto.DateOfBirth = DateTime.Today.AddYears(-10).ToString("yyyy-MM-dd");

        var errors = TestHelpers.Errors(await TestHelpers.CreateController(ctx).Create(dto));

        Assert.Equal(expected, errors["date_of_birth"]);
    }

    [Fact]
    public void On_leave_translations_mean_on_leave()
    {
        // regression: ta/te previously held unrelated words for "On Leave"
        Assert.Equal("விடுப்பில்", LoadResx("ta-IN")["OnLeave"]);
        Assert.Equal("సెలవులో", LoadResx("te-IN")["OnLeave"]);
    }
}
