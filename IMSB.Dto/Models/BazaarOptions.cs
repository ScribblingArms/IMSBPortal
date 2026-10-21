namespace IMSB.Dto.Models
{
    /// <summary>
    /// Bazaar-wide settings ("Bazaar" section of appsettings.json). Until stalls are stored in their own table,
    /// the stall capacity comes from here and a stall counts as occupied when inventory is assigned to it.
    /// </summary>
    public class BazaarOptions
    {
        public const string Section = "Bazaar";

        public int TotalStalls { get; set; } = 120;
    }
}
