using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IMSB.Dto.Models
{
    /// <summary>
    /// One staff member's attendance on one day (at most one row per staff per date). Hours and overtime are
    /// calculated from the in/out times when the record is saved (see <see cref="AttendanceRules"/>).
    /// </summary>
    [Table("attendance_records")]
    public class AttendanceRecord
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("staff_id")]
        public Guid StaffId { get; set; }

        public Staff? Staff { get; set; }

        [Column("attendance_date", TypeName = "date")]
        public DateTime AttendanceDate { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = AttendanceStatuses.Present;

        [Column("leave_without_pay")]
        public bool LeaveWithoutPay { get; set; }

        [Column("in_time")]
        public TimeSpan? InTime { get; set; }

        [Column("out_time")]
        public TimeSpan? OutTime { get; set; }

        [Column("worked_hours", TypeName = "decimal(5,2)")]
        public decimal WorkedHours { get; set; }

        [Column("overtime_hours", TypeName = "decimal(5,2)")]
        public decimal OvertimeHours { get; set; }

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("created_by")]
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("updated_by")]
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }

    public static class AttendanceStatuses
    {
        public const string Present = "PRESENT";
        public const string Absent = "ABSENT";
        public const string Leave = "LEAVE";
        public const string HalfDay = "HALF_DAY";

        public static readonly string[] All = { Present, Absent, Leave, HalfDay };

        /// <summary>Statuses that mean the person came in, so an in-time is required.</summary>
        public static bool Worked(string status) => status == Present || status == HalfDay;

        public static string? Normalize(string? status)
        {
            var s = status?.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');
            return s != null && All.Contains(s) ? s : null;
        }
    }

    /// <summary>Hours, overtime and attendance-rate calculations shared by the controller, the reports and the tests.</summary>
    public static class AttendanceRules
    {
        public const decimal DefaultShiftHours = 8m;

        private static readonly Regex ShiftRange = new(
            @"(\d{1,2})(?::(\d{2}))?\s*(AM|PM)\s*[-–]\s*(\d{1,2})(?::(\d{2}))?\s*(AM|PM)", RegexOptions.IgnoreCase);

        /// <summary>Start and end of a shift read from its name, e.g. "Morning (8AM-4PM)" → 08:00–16:00; null when the name has no times.</summary>
        public static (TimeSpan Start, TimeSpan End)? ShiftTimes(string? shiftName)
        {
            if (string.IsNullOrWhiteSpace(shiftName)) return null;
            var m = ShiftRange.Match(shiftName);
            if (!m.Success) return null;
            static TimeSpan? At(string h, string min, string ampm)
            {
                var hour = int.Parse(h, CultureInfo.InvariantCulture);
                var minute = string.IsNullOrEmpty(min) ? 0 : int.Parse(min, CultureInfo.InvariantCulture);
                if (hour < 1 || hour > 12 || minute > 59) return null;
                hour %= 12;
                if (ampm.Equals("PM", StringComparison.OrdinalIgnoreCase)) hour += 12;
                return new TimeSpan(hour, minute, 0);
            }
            var start = At(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
            var end = At(m.Groups[4].Value, m.Groups[5].Value, m.Groups[6].Value);
            return start == null || end == null ? null : (start.Value, end.Value);
        }

        /// <summary>Length of a shift in hours (overnight shifts wrap past midnight); the default when it cannot be read.</summary>
        public static decimal ShiftHours(string? shiftName)
        {
            var t = ShiftTimes(shiftName);
            if (t == null) return DefaultShiftHours;
            var length = t.Value.End - t.Value.Start;
            if (length <= TimeSpan.Zero) length += TimeSpan.FromDays(1);
            return Math.Round((decimal)length.TotalHours, 2);
        }

        /// <summary>Hours between in and out (0 until both are known); overtime is the part beyond the shift length.</summary>
        public static (decimal Worked, decimal Overtime) Hours(string status, TimeSpan? inTime, TimeSpan? outTime, decimal shiftHours)
        {
            if (!AttendanceStatuses.Worked(status) || inTime == null || outTime == null || outTime <= inTime) return (0m, 0m);
            var worked = Math.Round((decimal)(outTime.Value - inTime.Value).TotalHours, 2);
            var overtime = status == AttendanceStatuses.Present ? Math.Max(0m, worked - shiftHours) : 0m;
            return (worked, Math.Round(overtime, 2));
        }

        /// <summary>Attendance % of the marked records: present counts fully, half day as half; null when nothing is marked.</summary>
        public static decimal? Percent(int present, int halfDay, int marked) =>
            marked == 0 ? null : Math.Round((present + halfDay * 0.5m) * 100m / marked, 1);
    }
}
