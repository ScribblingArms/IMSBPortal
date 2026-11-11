using IMSB.Dto.Models;

namespace IMSB.Models
{
    /// <summary>Leaves/Add: the entry form with the year's entries and entitlements.</summary>
    public class LeaveEntryViewModel
    {
        public int Year { get; set; }
        public LeaveEntryForm Form { get; set; } = new();
        public List<LeaveStaffOption> Staff { get; set; } = new();
        public List<string> LeaveTypes { get; set; } = new();
        public List<LeaveEntitlement> Entitlements { get; set; } = new();
        public List<StaffLeaveEntry> Entries { get; set; } = new();
        /// <summary>The year's balances, shown when a staff member and leave type are picked.</summary>
        public List<LeaveBalanceRow> Balances { get; set; } = new();
    }
}
