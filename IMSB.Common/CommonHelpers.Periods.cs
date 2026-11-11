using System.Globalization;
using Microsoft.Extensions.Localization;

namespace IMSB.Common
{
    // Date-range checks used by the Admin and API controllers; messages come from the caller's SharedResource localizer
    public static partial class CommonHelpers
    {
        /// <summary>
        /// An optional from/to day pair (yyyy-MM-dd, empty = open). Errors keyed "from" / "to":
        /// ValDateInvalid for a bad date, ValDateRange when to is before from.
        /// </summary>
        public static Dictionary<string, string> ValidateRange(string? from, string? to, IStringLocalizer l,
            out DateTime? fromDay, out DateTime? toDay)
        {
            var errors = new Dictionary<string, string>();
            fromDay = toDay = null;
            if (!string.IsNullOrWhiteSpace(from))
            {
                if (TryParseDate(from, out var f)) fromDay = f;
                else errors["from"] = l["ValDateInvalid"];
            }
            if (!string.IsNullOrWhiteSpace(to))
            {
                if (TryParseDate(to, out var t)) toDay = t;
                else errors["to"] = l["ValDateInvalid"];
            }
            if (fromDay != null && toDay != null && fromDay > toDay) errors["to"] = l["ValDateRange"];
            return errors;
        }

        /// <summary>
        /// The period of an attendance list: from/to days when either is given (one alone is a single day),
        /// else a yyyy-MM month, else the current month. Errors keyed "from" / "to" / "month".
        /// </summary>
        public static Dictionary<string, string> ResolvePeriod(string? month, string? from, string? to, IStringLocalizer l,
            out DateTime start, out DateTime end)
        {
            var errors = new Dictionary<string, string>();
            var today = DateTime.Today;
            start = new DateTime(today.Year, today.Month, 1);
            end = start.AddMonths(1).AddDays(-1);

            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                DateTime f = default, t = default;
                if (!string.IsNullOrWhiteSpace(from) && !TryParseDate(from, out f)) errors["from"] = l["ValDateInvalid"];
                if (!string.IsNullOrWhiteSpace(to) && !TryParseDate(to, out t)) errors["to"] = l["ValDateInvalid"];
                if (errors.Count > 0) return errors;
                start = string.IsNullOrWhiteSpace(from) ? t : f;
                end = string.IsNullOrWhiteSpace(to) ? f : t;
                if (start > end) errors["to"] = l["ValDateRange"];
            }
            else if (!string.IsNullOrWhiteSpace(month))
            {
                if (DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m))
                {
                    start = m;
                    end = m.AddMonths(1).AddDays(-1);
                }
                else errors["month"] = l["ValDateInvalid"];
            }
            return errors;
        }
    }
}
