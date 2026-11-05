using IMSB.Dto.Models;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Backend.Mappers
{
    public static class OtAssignmentMapper
    {
        /// <summary>
        /// Both date-times are needed, with To later than From (the window may run past midnight).
        /// Returns the failing field and its SharedResource key, or null when valid.
        /// </summary>
        public static (string Field, string Key)? ValidateTimes(this OtAssignmentDto dto)
        {
            var fromGiven = !string.IsNullOrWhiteSpace(dto.FromDateTime);
            var toGiven = !string.IsNullOrWhiteSpace(dto.ToDateTime);
            if (!fromGiven || !TryParseDateTime(dto.FromDateTime, out var from)) return ("from_datetime", fromGiven ? "ValDateTime" : "ValOtTimes");
            if (!toGiven || !TryParseDateTime(dto.ToDateTime, out var to)) return ("to_datetime", toGiven ? "ValDateTime" : "ValOtTimes");
            return to <= from ? ("to_datetime", "ValOtToAfterFrom") : null;
        }

        /// <summary>The date-times of a form that passed <see cref="ValidateTimes"/>.</summary>
        public static (DateTime From, DateTime To) Times(this OtAssignmentDto dto)
        {
            TryParseDateTime(dto.FromDateTime, out var from);
            TryParseDateTime(dto.ToDateTime, out var to);
            return (from, to);
        }
    }
}
