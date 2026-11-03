using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Api.Controllers
{
    /// <summary>
    /// Staff attendance: a day's sheet (saved in bulk), department-wise figures, history with edit / delete of single
    /// records, and the monthly calendar. Dates are yyyy-MM-dd, months yyyy-MM, times HH:mm.
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    public class AttendanceController : ApiControllerBase
    {
        public const int MaxEntriesPerSave = 1000;

        private readonly IAttendanceInterface _attendance;

        public AttendanceController(IAttendanceInterface attendance) => _attendance = attendance;

        /// <summary>A day's sheet (default today), its counts, the week strip and each department's figures.</summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? date, [FromQuery] string? departmentId)
        {
            var day = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(date) && !TryParseDate(date, out day)) return Invalid("date", "ValDateInvalid");

            var sheet = await _attendance.SheetAsync(day);
            var rows = Guid.TryParse(departmentId, out var deptId) ? sheet.Where(r => r.Staff.DepartmentId == deptId).ToList() : sheet;
            // month to date, per department
            var month = (await _attendance.StatusesBetweenAsync(new DateTime(day.Year, day.Month, 1), day)).ToLookup(r => r.DepartmentId);
            var sheetByDept = sheet.ToLookup(r => r.Staff.DepartmentId);

            return Ok(new
            {
                date = Date(day),
                today = Date(DateTime.Today),
                isFuture = day > DateTime.Today,
                stats = Counts(sheet),
                week = await WeekAsync(day),
                rows = rows.Select(r => new
                {
                    employeeId = r.Staff.EmployeeId,
                    name = r.Staff.FullName,
                    departmentId = r.Staff.DepartmentId.ToString(),
                    departmentName = r.DepartmentName,
                    designation = r.Staff.Designation,
                    shift = r.ShiftName ?? r.Staff.Shift,
                    shiftStart = Time(AttendanceRules.ShiftTimes(r.ShiftName)?.Start),
                    shiftEnd = Time(AttendanceRules.ShiftTimes(r.ShiftName)?.End),
                    shiftHours = AttendanceRules.ShiftHours(r.ShiftName),
                    staffStatus = r.Staff.Status.ToUpperInvariant(),
                    record = r.Record == null ? null : RecordDto(r.Record)
                }),
                departments = (await _attendance.DepartmentsAsync()).Select(d =>
                {
                    var c = Counts(sheetByDept[d.Id]);
                    var m = month[d.Id].ToList();
                    return new
                    {
                        id = d.Id.ToString(),
                        name = d.Name,
                        c.Total, c.Present, c.Absent, c.Leave, c.HalfDay, c.NotMarked, c.WithOvertime, c.Percent,
                        monthPercent = AttendanceRules.Percent(m.Count(r => r.Status == AttendanceStatuses.Present),
                            m.Count(r => r.Status == AttendanceStatuses.HalfDay), m.Count)
                    };
                })
            });
        }

        /// <summary>Saves the marked rows of a day's sheet: a record already saved for that staff and date is updated, others are added.</summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] AttendanceSheetDto dto)
        {
            var errors = new Dictionary<string, string>();
            var day = DateTime.MinValue;
            if (!TryParseDate(dto.Date, out day)) errors["date"] = L["ValDateInvalid"];
            else if (day > DateTime.Today) errors["date"] = L["ValDateFuture"];

            var entries = dto.Entries ?? new List<AttendanceEntryDto>();
            if (entries.Count == 0) { errors["entries"] = L["NothingToSave"]; return Invalid(errors); }
            if (entries.Count > MaxEntriesPerSave) { errors["entries"] = L["ValTooManyEntries", MaxEntriesPerSave]; return Invalid(errors); }

            var ids = entries.Select(e => e.EmployeeId?.Trim().ToUpperInvariant() ?? "").Where(id => id != "").Distinct().ToList();
            var staff = (await _attendance.StaffByEmployeeIdsAsync(ids)).ToDictionary(s => s.EmployeeId, StringComparer.OrdinalIgnoreCase);
            var shifts = await _attendance.ShiftNamesAsync();

            var lines = new List<(Staff Staff, string Status, TimeSpan? In, TimeSpan? Out, string? Remarks)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var prefix = $"entries_{i}_";
                var id = e.EmployeeId?.Trim() ?? "";
                if (!staff.TryGetValue(id, out var s)) { errors.TryAdd(prefix + "employee_id", L["ValStaffNotFound"]); continue; }
                if (!seen.Add(id)) { errors.TryAdd(prefix + "employee_id", L["ValDuplicateStaff"]); continue; }
                if (day != DateTime.MinValue && s.JoinDate.Date > day) { errors.TryAdd(prefix + "employee_id", L["ValBeforeJoining"]); continue; }

                var (status, inTime, outTime) = ValidateEntry(e.Status, e.InTime, e.OutTime, prefix, errors);
                if (status != null) lines.Add((s, status, inTime, outTime, CleanRemarks(e.Remarks)));
            }
            if (errors.Count > 0) return Invalid(errors);

            var existing = await _attendance.RecordsOnAsync(day, lines.Select(l => l.Staff.Id).ToList());
            var created = new List<AttendanceRecord>();
            var saved = new List<AttendanceRecord>();
            foreach (var l in lines)
            {
                if (!existing.TryGetValue(l.Staff.Id, out var record))
                {
                    record = new AttendanceRecord { StaffId = l.Staff.Id, AttendanceDate = day, CreatedBy = UserName };
                    created.Add(record);
                }
                else
                {
                    record.UpdatedAt = DateTime.UtcNow;
                    record.UpdatedBy = UserName;
                }
                Apply(record, l.Status, l.In, l.Out, l.Remarks, shifts.GetValueOrDefault(l.Staff.Shift.Trim().ToUpperInvariant()));
                saved.Add(record);
            }
            var lopCount = await _attendance.ApplyLeavePolicyAsync(saved);
            await _attendance.SaveSheetAsync(created);
            return Ok(new { date = Date(day), saved = lines.Count, created = created.Count, updated = lines.Count - created.Count, lopCount });
        }

        public record HistoryRow(string Id, string Date, string EmployeeId, string Name, string DepartmentId, string DepartmentName, string Designation,
            string Status, string? InTime, string? OutTime, decimal WorkedHours, decimal OvertimeHours, string? Remarks, string? By,
            bool LeaveWithoutPay);

        [HttpGet("records/{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var record = await _attendance.FindAsync(id, tracking: false);
            return record == null ? NotFound() : Ok(ToHistoryRow(record, await _attendance.DepartmentNamesAsync()));
        }

        [HttpPut("records/{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AttendanceUpdateDto dto)
        {
            var record = await _attendance.FindAsync(id, tracking: true);
            if (record == null) return NotFound();

            var errors = new Dictionary<string, string>();
            var (status, inTime, outTime) = ValidateEntry(dto.Status, dto.InTime, dto.OutTime, "", errors);
            if (errors.Count > 0 || status == null) return Invalid(errors);

            var shifts = await _attendance.ShiftNamesAsync();
            Apply(record, status, inTime, outTime, CleanRemarks(dto.Remarks), shifts.GetValueOrDefault(record.Staff!.Shift.Trim().ToUpperInvariant()));
            await _attendance.ApplyLeavePolicyAsync(new[] { record });
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = UserName;
            await _attendance.UpdateAsync(record);
            return Ok(ToHistoryRow(record, await _attendance.DepartmentNamesAsync()));
        }

        [HttpDelete("records/{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _attendance.FindAsync(id, tracking: true);
            if (record == null) return NotFound();
            await _attendance.RemoveAsync(record);
            return Ok(new { id = record.Id.ToString(), employeeId = record.Staff?.EmployeeId, date = Date(record.AttendanceDate) });
        }

        /// <summary>Saved records for from..to when given, otherwise the month (yyyy-MM), otherwise the current month.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? month, [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ResolvePeriod(month, from, to, out var start, out var end);
            if (errors.Count > 0) return Invalid(errors);

            var depts = await _attendance.DepartmentNamesAsync();
            var rows = (await _attendance.HistoryAsync(q, departmentId, status, start, end)).Select(r => ToHistoryRow(r, depts)).ToList();
            int Count(string s) => rows.Count(r => r.Status == s);
            return Ok(new
            {
                from = Date(start),
                to = Date(end),
                rows,
                totals = new
                {
                    records = rows.Count,
                    present = Count(AttendanceStatuses.Present),
                    absent = Count(AttendanceStatuses.Absent),
                    leave = Count(AttendanceStatuses.Leave),
                    halfDay = Count(AttendanceStatuses.HalfDay),
                    hours = rows.Sum(r => r.WorkedHours),
                    overtime = rows.Sum(r => r.OvertimeHours),
                    percent = AttendanceRules.Percent(Count(AttendanceStatuses.Present), Count(AttendanceStatuses.HalfDay), rows.Count)
                }
            });
        }

        /// <summary>The month's calendar (default this month); type = designation, status = a status or NOT_MARKED.</summary>
        [HttpGet("monthly")]
        public async Task<IActionResult> Monthly([FromQuery] string? month, [FromQuery] string? type, [FromQuery] string? q, [FromQuery] string? status)
        {
            var key = string.IsNullOrWhiteSpace(month) ? DateTime.Today.ToString("yyyy-MM") : month.Trim();
            if (!DateTime.TryParseExact(key, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                return Invalid("month", "ValDateInvalid");

            string? normalizedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                normalizedStatus = status.Trim().ToUpperInvariant();
                if (normalizedStatus is not (AttendanceStatuses.Present or AttendanceStatuses.Absent or AttendanceStatuses.Leave
                    or AttendanceStatuses.HalfDay or "NOT_MARKED"))
                    return Invalid("status", "ValMonthlyAttendanceStatus");
            }
            return Ok(await _attendance.MonthlyCalendarAsync(start, type, q, normalizedStatus));
        }

        // ── helpers ──────────────────────────────────────────────────

        private static AttendanceCounts Counts(IEnumerable<SheetRow> rows)
        {
            var list = rows.ToList();
            int Count(string status) => list.Count(r => r.Record?.Status == status);
            var present = Count(AttendanceStatuses.Present);
            var halfDay = Count(AttendanceStatuses.HalfDay);
            var marked = list.Count(r => r.Record != null);
            return new AttendanceCounts(list.Count, present, Count(AttendanceStatuses.Absent), Count(AttendanceStatuses.Leave), halfDay,
                list.Count - marked, list.Count(r => r.Record?.OvertimeHours > 0), AttendanceRules.Percent(present, halfDay, marked));
        }

        /// <summary>Monday to Sunday around the day, with each day's attendance % of the records marked that day.</summary>
        private async Task<object> WeekAsync(DateTime day)
        {
            var start = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            var records = (await _attendance.StatusesBetweenAsync(start, start.AddDays(6))).ToLookup(r => r.Date);
            return new
            {
                start = Date(start),
                end = Date(start.AddDays(6)),
                days = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).Select(d =>
                {
                    var onDay = records[d].ToList();
                    return new
                    {
                        date = Date(d),
                        marked = onDay.Count,
                        percent = AttendanceRules.Percent(onDay.Count(r => r.Status == AttendanceStatuses.Present),
                            onDay.Count(r => r.Status == AttendanceStatuses.HalfDay), onDay.Count),
                        future = d > DateTime.Today
                    };
                })
            };
        }

        /// <summary>Checks status and times; absent / leave keep no times. Errors are keyed prefix + status | in_time | out_time.</summary>
        private (string? Status, TimeSpan? In, TimeSpan? Out) ValidateEntry(string? status, string? inTime, string? outTime, string prefix, Dictionary<string, string> errors)
        {
            var st = AttendanceStatuses.Normalize(status);
            if (st == null) { errors.TryAdd(prefix + "status", L["ValAttendanceStatus"]); return (null, null, null); }
            if (!AttendanceStatuses.Worked(st)) return (st, null, null);

            TimeSpan? inT = null, outT = null;
            if (string.IsNullOrWhiteSpace(inTime)) errors.TryAdd(prefix + "in_time", L["ValInTimeRequired"]);
            else if (TryParseTime(inTime, out var a)) inT = a;
            else errors.TryAdd(prefix + "in_time", L["ValTime"]);

            if (!string.IsNullOrWhiteSpace(outTime))
            {
                if (TryParseTime(outTime, out var b)) outT = b;
                else errors.TryAdd(prefix + "out_time", L["ValTime"]);
            }
            if (inT != null && outT != null && outT <= inT) errors.TryAdd(prefix + "out_time", L["ValOutBeforeIn"]);
            return (st, inT, outT);
        }

        private static void Apply(AttendanceRecord record, string status, TimeSpan? inTime, TimeSpan? outTime, string? remarks, string? shiftName)
        {
            record.Status = status;
            record.InTime = inTime;
            record.OutTime = outTime;
            (record.WorkedHours, record.OvertimeHours) = AttendanceRules.Hours(status, inTime, outTime, AttendanceRules.ShiftHours(shiftName));
            record.Remarks = remarks;
        }

        private Dictionary<string, string> ResolvePeriod(string? month, string? from, string? to, out DateTime start, out DateTime end)
        {
            var errors = new Dictionary<string, string>();
            start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            end = start.AddMonths(1).AddDays(-1);

            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                DateTime f = default, t = default;
                if (!string.IsNullOrWhiteSpace(from) && !TryParseDate(from, out f)) errors["from"] = L["ValDateInvalid"];
                if (!string.IsNullOrWhiteSpace(to) && !TryParseDate(to, out t)) errors["to"] = L["ValDateInvalid"];
                if (errors.Count > 0) return errors;
                start = string.IsNullOrWhiteSpace(from) ? t : f;
                end = string.IsNullOrWhiteSpace(to) ? f : t;
                if (start > end) errors["to"] = L["ValDateRange"];
            }
            else if (!string.IsNullOrWhiteSpace(month))
            {
                if (DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m))
                {
                    start = m;
                    end = m.AddMonths(1).AddDays(-1);
                }
                else errors["month"] = L["ValDateInvalid"];
            }
            return errors;
        }

        private static HistoryRow ToHistoryRow(AttendanceRecord r, Dictionary<Guid, string> depts) => new(
            r.Id.ToString(), Date(r.AttendanceDate), r.Staff?.EmployeeId ?? string.Empty, r.Staff?.FullName ?? string.Empty,
            r.Staff?.DepartmentId.ToString() ?? string.Empty, r.Staff == null ? string.Empty : depts.GetValueOrDefault(r.Staff.DepartmentId) ?? string.Empty,
            r.Staff?.Designation ?? string.Empty, r.Status, Time(r.InTime), Time(r.OutTime), r.WorkedHours, r.OvertimeHours, r.Remarks,
            r.UpdatedBy ?? r.CreatedBy, r.LeaveWithoutPay);

        private static object RecordDto(AttendanceRecord r) => new
        {
            id = r.Id.ToString(),
            status = r.Status,
            leaveWithoutPay = r.LeaveWithoutPay,
            inTime = Time(r.InTime),
            outTime = Time(r.OutTime),
            workedHours = r.WorkedHours,
            overtimeHours = r.OvertimeHours,
            remarks = r.Remarks,
            by = r.UpdatedBy ?? r.CreatedBy
        };

        private static string? CleanRemarks(string? remarks) => string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        private static bool TryParseTime(string? value, out TimeSpan time) =>
            TimeSpan.TryParseExact(value?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time) && time < TimeSpan.FromDays(1);

        private static string? Time(TimeSpan? t) => t?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}
