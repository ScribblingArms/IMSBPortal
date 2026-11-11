namespace IMSB.Models
{
    /// <summary>The figures on the login page, already formatted for display.</summary>
    public sealed record LoginPageStats(
        string ActiveStalls,
        string Artisans,
        string MonthlyRevenue,
        string ProductCategories);
}
