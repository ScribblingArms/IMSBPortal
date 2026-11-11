using IMSB.Dto.Models;

namespace IMSB.Models
{
    /// <summary>Leaves/Index: the year's entitlements and balances.</summary>
    public class LeaveManagementViewModel
    {
        public int Year { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<LeaveStaffOption> Staff { get; set; } = new();
        public List<LeaveEntitlement> Entitlements { get; set; } = new();
        public List<LeaveBalanceRow> Balances { get; set; } = new();
    }
}
