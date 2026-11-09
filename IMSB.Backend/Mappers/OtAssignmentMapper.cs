using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Backend.Mappers
{
    public static class OtAssignmentMapper
    {
        /// <summary>
        /// Both date-times are needed. The OT date (the From date) must fall between <paramref name="today"/> and the last day
        /// of its month; To must be later than From and less than 24 hours after it (the window may run past midnight).
        /// Returns the failing field and its SharedResource key, or null when valid.
        /// </summary>
        public static (string Field, string Key)? ValidateTimes(this OtAssignmentDto dto, DateTime today)
        {
            var fromGiven = !string.IsNullOrWhiteSpace(dto.FromDateTime);
            var toGiven = !string.IsNullOrWhiteSpace(dto.ToDateTime);
            if (!fromGiven || !TryParseDateTime(dto.FromDateTime, out var from)) return ("from_datetime", fromGiven ? "ValDateTime" : "ValOtTimes");
            if (!toGiven || !TryParseDateTime(dto.ToDateTime, out var to)) return ("to_datetime", toGiven ? "ValDateTime" : "ValOtTimes");
            if (from.Date < today.Date) return ("ot_date", "ValOtDatePast");
            if (from.Date > MonthEnd(today)) return ("ot_date", "ValOtDateThisMonth");
            if (to <= from) return ("to_datetime", "ValOtToAfterFrom");
            return to >= from.AddDays(1) ? ("to_datetime", "ValOtOneDay") : null;
        }

        /// <summary>The date-times of a form that passed <see cref="ValidateTimes"/>.</summary>
        public static (DateTime From, DateTime To) Times(this OtAssignmentDto dto)
        {
            TryParseDateTime(dto.FromDateTime, out var from);
            TryParseDateTime(dto.ToDateTime, out var to);
            return (from, to);
        }

        /// <summary>
        /// When OT on <paramref name="otDate"/> may run for a staff member on a shift: from the end of the shift that ends that
        /// day until the next shift starts. A day shift 08:00–16:00 gives 16:00 → 08:00 next day; an overnight shift
        /// 22:00–06:00 gives 06:00 → 22:00 the same day.
        /// </summary>
        public static (DateTime From, DateTime Until) OtWindow(DateTime otDate, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            var shiftEnds = otDate.Date + shiftEnd;
            var nextShift = otDate.Date + shiftStart;
            if (nextShift <= shiftEnds) nextShift = nextShift.AddDays(1);
            return (shiftEnds, nextShift);
        }

        /// <summary>
        /// The SharedResource key when the OT window falls inside the staff member's regular shift (it must start once the
        /// shift has ended and finish by the next shift's start), or null when it is clear or the shift timings are unknown.
        /// </summary>
        public static string? ShiftConflict(DateTime from, DateTime to, (TimeSpan Start, TimeSpan End)? shift)
        {
            if (shift == null) return null;
            var window = OtWindow(from.Date, shift.Value.Start, shift.Value.End);
            if (from < window.From) return "ValOtBeforeShiftEnd";
            return to > window.Until ? "ValOtIntoNextShift" : null;
        }

        /// <summary>The form field a refusal from IMSB.Backend.OtAssignments belongs to.</summary>
        public static string ErrorField(string errorKey, string fallback) => errorKey switch
        {
            "ValOtBeforeShiftEnd" => "from_datetime",
            "ValOtIntoNextShift" => "to_datetime",
            "ValOtPastLocked" => "request",
            _ => fallback
        };

        /// <summary>The last day of the month <paramref name="today"/> is in: the latest date OT can be assigned for.</summary>
        public static DateTime MonthEnd(DateTime today) =>
            new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
    }
}
