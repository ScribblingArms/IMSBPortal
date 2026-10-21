using System.Globalization;
using System.Runtime.CompilerServices;

namespace IMSB.Admin.Tests;

internal static class TestCulture
{
    // The app always resolves to one of its supported cultures (default en-US). There is no neutral
    // SharedResource.resx, so pin the tests to en-US instead of inheriting the machine's culture.
    [ModuleInitializer]
    internal static void Init()
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }
}
