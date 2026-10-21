namespace IMSB.Dto.Models
{
    // Demo accounts seeded and shown on the login page in the Development environment only.
    public static class DemoUsers
    {
        public record DemoUser(string Role, string Username, string Password);

        public static readonly IReadOnlyList<DemoUser> All = new[]
        {
            new DemoUser("Admin", "admin@imsb.tn.gov.in", "Admin@2025"),
            new DemoUser("Staff", "staff@imsb.tn.gov.in", "Staff@2025"),
            new DemoUser("Inventory", "inventory@imsb.tn.gov.in", "Inventory@2025")
        };
    }
}
