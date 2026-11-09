using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    /// <summary>A staff member's approved OT for a month and what it pays.</summary>
    public record OvertimeResult(decimal Hours, decimal Amount)
    {
        public static readonly OvertimeResult None = new(0m, 0m);
    }

    /// <summary>
    /// OT pay, shared by the Monthly Reports, the Salary Report and the Pay Bill.
    /// <list type="bullet">
    /// <item>Approved OT hours: the OT Hrs saved on the staff member's OT assignments (ot_assignments.OtHours), totalled
    /// for the month the OT date (From) falls in.</item>
    /// <item>Housekeeping, and every category other than Security: ₹50 per hour.</item>
    /// <item>Security (Staff Category name containing "Security"): a day's pay is monthly salary ÷ 30, and the hours are
    /// turned into days by the employee's shift length. ₹16,500 on an 8-hour shift: ₹550 a day, so 4 hours = ₹275.</item>
    /// </list>
    /// </summary>
    public static class OvertimePay
    {
        /// <summary>Housekeeping OT rate: OT Amount = approved OT hours × ₹50. Also used for every category other than Security.</summary>
        public const decimal HousekeepingHourlyRate = 50m;
        public const decimal HourlyRate = HousekeepingHourlyRate;
        public const decimal SecurityDaysPerMonth = 30m;
        public const string SecurityCategory = "Security";
        public const string HousekeepingCategory = "Housekeeping";

        public static bool IsSecurity(string? staffCategory) =>
            staffCategory != null && staffCategory.Contains(SecurityCategory, StringComparison.OrdinalIgnoreCase);

        /// <summary>"Housekeeping", "House Keeping", "HOUSEKEEPING STAFF" … (spaces and case ignored).</summary>
        public static bool IsHousekeeping(string? staffCategory) =>
            staffCategory != null && staffCategory.Replace(" ", "").Contains(HousekeepingCategory, StringComparison.OrdinalIgnoreCase);

        /// <summary>Housekeeping OT pay: approved OT hours × ₹50, rounded to the paisa.</summary>
        public static decimal HousekeepingAmount(decimal hours) =>
            hours <= 0m ? 0m : Math.Round(hours * HousekeepingHourlyRate, 2, MidpointRounding.AwayFromZero);

        /// <summary>The pay for <paramref name="hours"/> of approved OT, rounded to the paisa.</summary>
        public static decimal Amount(decimal hours, string? staffCategory, decimal monthlySalary, decimal shiftHours)
        {
            if (hours <= 0m) return 0m;
            if (!IsSecurity(staffCategory)) return HousekeepingAmount(hours);   // Housekeeping, and every other category
            var amount = monthlySalary / SecurityDaysPerMonth * hours / (shiftHours > 0m ? shiftHours : AttendanceRules.DefaultShiftHours);
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>The month's approved OT hours: the saved OT Hrs of the assignments whose OT date is in that month.</summary>
        public static decimal MonthHours(IEnumerable<OtAssignment> assignments, DateTime monthStart)
        {
            var monthEnd = monthStart.AddMonths(1);
            return assignments.Where(a => a.FromDateTime >= monthStart && a.FromDateTime < monthEnd).Sum(a => a.OtHours);
        }

        /// <summary>Approved OT hours (saved on the OT assignments) and their pay for each staff member in the month.</summary>
        public static async Task<Dictionary<Guid, OvertimeResult>> ForMonthAsync(IMSBContext context, IReadOnlyCollection<Staff> staff,
            IReadOnlyDictionary<Guid, string> categories, DateTime monthStart)
        {
            var staffIds = staff.Select(s => s.Id).ToList();
            var monthEnd = monthStart.AddMonths(1);
            var assignments = (await context.OtAssignments.AsNoTracking()
                    .Where(a => staffIds.Contains(a.StaffId) && a.FromDateTime >= monthStart && a.FromDateTime < monthEnd)
                    .ToListAsync())
                .ToLookup(a => a.StaffId);
            var shifts = (await context.Shifts.AsNoTracking().ToListAsync())
                .GroupBy(s => s.Code.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Name);

            return staff.ToDictionary(s => s.Id, s =>
            {
                var hours = MonthHours(assignments[s.Id], monthStart);
                if (hours == 0m) return OvertimeResult.None;
                var shiftName = string.IsNullOrWhiteSpace(s.Shift) ? null : shifts.GetValueOrDefault(s.Shift.Trim().ToUpperInvariant()) ?? s.Shift;
                var amount = Amount(hours, categories.GetValueOrDefault(s.DepartmentId), s.MonthlySalary, AttendanceRules.ShiftHours(shiftName));
                return new OvertimeResult(hours, amount);
            });
        }
    }
}
