using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text;
using static IMSB.Common.CommonHelpers;

namespace IMSB.Admin.Controllers
{
    /// <summary>
    /// Staff attendance: the day's Attendance Entry sheet (saved in bulk), department-wise figures, the Attendance
    /// History list with view / edit / delete of single records, and CSV export.
    /// </summary>
    [Authorize(Roles = "Staff,Admin")]
    public class AttendanceController : Controller
    {
        public const int MaxEntriesPerSave = 1000;

        private readonly IAttendanceInterface _attendance;
        private readonly IStringLocalizer _L;

        public AttendanceController(IAttendanceInterface attendance, IStringLocalizerFactory localizerFactory)
        {
            _attendance = attendance;
            // Same shared resources the views use (Resources/SharedResource.<culture>.resx)
            _L = localizerFactory.Create("SharedResource", typeof(AttendanceController).Assembly.GetName().Name!);
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = _L["Attendance"].Value;
            ViewBag.Departments = (await _attendance.DepartmentsAsync())
                .Select(d => new KeyValuePair<string, string>(d.Id.ToString(), d.Name)).ToList();
            return View();
        }

        // ── Attendance Entry / Department-wise ────────────────────────

        /// <summary>
        /// Everything the Attendance Entry and Department-wise tabs need for one day: the sheet (staff who were employed
        /// and not inactive that day, plus anyone already marked), the day's counts, the week strip and each department.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Data([FromQuery] string? date, [FromQuery] string? departmentId)
        {
            DateTime day = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(date) && !TryParseDate(date, out day))
                return BadRequest(new { errors = new Dictionary<string, string> { ["date"] = _L["ValDateInvalid"] } });

            var sheet = await SheetAsync(day);
            var rows = Guid.TryParse(departmentId, out var deptId) ? sheet.Where(r => r.Staff.DepartmentId == deptId).ToList() : sheet;

            // month to date, per department
            var monthStart = new DateTime(day.Year, day.Month, 1);
            var month = (await _attendance.StatusesBetweenAsync(monthStart, day)).ToLookup(r => r.DepartmentId);
            var sheetByDept = sheet.ToLookup(r => r.Staff.DepartmentId);
            var departments = await _attendance.DepartmentsAsync();

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
                departments = departments.Select(d =>
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

        internal Task<List<SheetRow>> SheetAsync(DateTime day) => _attendance.SheetAsync(day);

        internal static AttendanceCounts Counts(IEnumerable<SheetRow> rows)
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
            var end = start.AddDays(6);
            var records = (await _attendance.StatusesBetweenAsync(start, end)).ToLookup(r => r.Date);
            return new
            {
                start = Date(start),
                end = Date(end),
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

        /// <summary>Saves the marked rows of a day's sheet: a row already saved for that staff and date is updated, others are added.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] AttendanceSheetDto? dto)
        {
            if (dto == null) return BadRequest();
            var errors = BindingErrors();
            var day = DateTime.MinValue;
            if (!errors.ContainsKey("date"))
            {
                if (!TryParseDate(dto.Date, out day)) errors["date"] = _L["ValDateInvalid"];
                else if (day > DateTime.Today) errors["date"] = _L["ValDateFuture"];
            }

            var entries = dto.Entries ?? new List<AttendanceEntryDto>();
            if (entries.Count == 0) { errors["entries"] = _L["NothingToSave"]; return BadRequest(new { errors }); }
            if (entries.Count > MaxEntriesPerSave) { errors["entries"] = string.Format(_L["ValTooManyEntries"], MaxEntriesPerSave); return BadRequest(new { errors }); }

            var ids = entries.Select(e => e.EmployeeId?.Trim().ToUpperInvariant() ?? "").Where(id => id != "").Distinct().ToList();
            var staff = (await _attendance.StaffByEmployeeIdsAsync(ids))
                .ToDictionary(s => s.EmployeeId, StringComparer.OrdinalIgnoreCase);
            var shifts = await _attendance.ShiftNamesAsync();

            var lines = new List<(Staff Staff, string Status, TimeSpan? In, TimeSpan? Out, string? Remarks)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var prefix = $"entries_{i}_";
                var id = e.EmployeeId?.Trim() ?? "";
                if (!staff.TryGetValue(id, out var s)) { errors.TryAdd(prefix + "employee_id", _L["ValStaffNotFound"]); continue; }
                if (!seen.Add(id)) { errors.TryAdd(prefix + "employee_id", _L["ValDuplicateStaff"]); continue; }
                if (day != DateTime.MinValue && s.JoinDate.Date > day) { errors.TryAdd(prefix + "employee_id", _L["ValBeforeJoining"]); continue; }

                var (status, inTime, outTime) = ValidateEntry(e.Status, e.InTime, e.OutTime, prefix, errors);
                if (status != null) lines.Add((s, status, inTime, outTime, CleanRemarks(e.Remarks)));
            }
            if (errors.Count > 0) return BadRequest(new { errors });

            var staffIds = lines.Select(l => l.Staff.Id).ToList();
            var existing = await _attendance.RecordsOnAsync(day, staffIds);
            var user = User.Identity?.Name;
            int created = 0, updated = 0;
            foreach (var l in lines)
            {
                if (!existing.TryGetValue(l.Staff.Id, out var record))
                {
                    record = new AttendanceRecord { StaffId = l.Staff.Id, AttendanceDate = day, CreatedBy = user };
                    _attendance.Add(record);
                    created++;
                }
                else
                {
                    record.UpdatedAt = DateTime.UtcNow;
                    record.UpdatedBy = user;
                    updated++;
                }
                Apply(record, l.Status, l.In, l.Out, l.Remarks, shifts.GetValueOrDefault(l.Staff.Shift.Trim().ToUpperInvariant()));
            }
            await _attendance.SaveChangesAsync();
            return Ok(new { date = Date(day), saved = lines.Count, created, updated });
        }

        // ── Single records (view / edit / delete) ─────────────────────

        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            var record = await FindAsync(id, tracking: false);
            return record == null ? NotFound() : Ok(await HistoryDtoAsync(record));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, [FromBody] AttendanceUpdateDto? dto)
        {
            if (dto == null) return BadRequest();
            var record = await FindAsync(id, tracking: true);
            if (record == null) return NotFound();

            var errors = BindingErrors();
            var (status, inTime, outTime) = ValidateEntry(dto.Status, dto.InTime, dto.OutTime, "", errors);
            if (errors.Count > 0 || status == null) return BadRequest(new { errors });

            var shifts = await _attendance.ShiftNamesAsync();
            Apply(record, status, inTime, outTime, CleanRemarks(dto.Remarks), shifts.GetValueOrDefault(record.Staff!.Shift.Trim().ToUpperInvariant()));
            record.UpdatedAt = DateTime.UtcNow;
            record.UpdatedBy = User.Identity?.Name;
            await _attendance.SaveChangesAsync();
            return Ok(await HistoryDtoAsync(record));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var record = await FindAsync(id, tracking: true);
            if (record == null) return NotFound();
            _attendance.Remove(record);
            await _attendance.SaveChangesAsync();
            return Ok(new { id = record.Id.ToString(), employeeId = record.Staff?.EmployeeId, date = Date(record.AttendanceDate) });
        }

        private async Task<AttendanceRecord?> FindAsync(string id, bool tracking) =>
            Guid.TryParse(id, out var guid) ? await _attendance.FindAsync(guid, tracking) : null;

        // ── Attendance History ────────────────────────────────────────

        internal record HistoryRow(string Id, string Date, string EmployeeId, string Name, string DepartmentId, string DepartmentName, string Designation,
            string Status, string? InTime, string? OutTime, decimal WorkedHours, decimal OvertimeHours, string? Remarks, string? By);

        /// <summary>
        /// Saved records for a period with every filter the History tab offers. The period is from/to when given,
        /// otherwise the month (yyyy-MM), otherwise the current month.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> History([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? month, [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ResolvePeriod(month, from, to, out var start, out var end);
            if (errors.Count > 0) return BadRequest(new { errors });

            var rows = await HistoryRowsAsync(q, departmentId, status, start, end);
            return Ok(new
            {
                from = Date(start),
                to = Date(end),
                rows,
                totals = new
                {
                    records = rows.Count,
                    present = rows.Count(r => r.Status == AttendanceStatuses.Present),
                    absent = rows.Count(r => r.Status == AttendanceStatuses.Absent),
                    leave = rows.Count(r => r.Status == AttendanceStatuses.Leave),
                    halfDay = rows.Count(r => r.Status == AttendanceStatuses.HalfDay),
                    hours = rows.Sum(r => r.WorkedHours),
                    overtime = rows.Sum(r => r.OvertimeHours),
                    percent = AttendanceRules.Percent(rows.Count(r => r.Status == AttendanceStatuses.Present),
                        rows.Count(r => r.Status == AttendanceStatuses.HalfDay), rows.Count)
                }
            });
        }

        internal async Task<List<HistoryRow>> HistoryRowsAsync(string? q, string? departmentId, string? status, DateTime from, DateTime to)
        {
            var records = await _attendance.HistoryAsync(q, departmentId, status, from, to);
            var depts = await _attendance.DepartmentNamesAsync();
            return records.Select(r => ToHistoryRow(r, depts)).ToList();
        }

        private static HistoryRow ToHistoryRow(AttendanceRecord r, Dictionary<Guid, string> depts) => new(
            r.Id.ToString(), Date(r.AttendanceDate), r.Staff?.EmployeeId ?? string.Empty, r.Staff?.FullName ?? string.Empty,
            r.Staff?.DepartmentId.ToString() ?? string.Empty, r.Staff == null ? string.Empty : depts.GetValueOrDefault(r.Staff.DepartmentId) ?? string.Empty,
            r.Staff?.Designation ?? string.Empty, r.Status, Time(r.InTime), Time(r.OutTime), r.WorkedHours, r.OvertimeHours, r.Remarks,
            r.UpdatedBy ?? r.CreatedBy);

        private async Task<HistoryRow> HistoryDtoAsync(AttendanceRecord r) =>
            ToHistoryRow(r, await _attendance.DepartmentNamesAsync());

        /// <summary>History as CSV, with the same filters (the page's header button exports the selected day).</summary>
        [HttpGet]
        public async Task<IActionResult> Export([FromQuery] string? q, [FromQuery] string? departmentId, [FromQuery] string? status,
            [FromQuery] string? month, [FromQuery] string? from, [FromQuery] string? to)
        {
            var errors = ResolvePeriod(month, from, to, out var start, out var end);
            if (errors.Count > 0) return BadRequest(new { errors });
            var rows = await HistoryRowsAsync(q, departmentId, status, start, end);

            var sb = new StringBuilder();
            sb.AppendLine(Row(_L["Date"], _L["EmployeeID"], _L["StaffName"], _L["Department"], _L["Designation"], _L["InTime"], _L["OutTime"],
                _L["Hours"], _L["OTHours"], _L["Status"], _L["Remarks"], _L["UpdatedBy"]));
            foreach (var r in rows.OrderBy(r => r.Date).ThenBy(r => r.DepartmentName).ThenBy(r => r.Name))
            {
                sb.AppendLine(Row(r.Date, r.EmployeeId, r.Name, r.DepartmentName, r.Designation, r.InTime, r.OutTime,
                    Hours(r.WorkedHours), Hours(r.OvertimeHours), StatusLabel(_L, r.Status), r.Remarks, r.By));
            }
            sb.AppendLine(Row(_L["Total"], "", "", "", "", "", "", Hours(rows.Sum(r => r.WorkedHours)), Hours(rows.Sum(r => r.OvertimeHours)), "", "", ""));
            return Csv(sb, $"attendance_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
        }

        // ── helpers ──────────────────────────────────────────────────

        internal static string StatusLabel(IStringLocalizer L, string status) => status switch
        {
            AttendanceStatuses.Present => L["Present"],
            AttendanceStatuses.Absent => L["Absent"],
            AttendanceStatuses.Leave => L["Leave"],
            AttendanceStatuses.HalfDay => L["HalfDay"],
            _ => status
        };

        /// <summary>Checks status and times; absent / leave keep no times. Errors are keyed prefix + status | in_time | out_time.</summary>
        private (string? Status, TimeSpan? In, TimeSpan? Out) ValidateEntry(string? status, string? inTime, string? outTime, string prefix, Dictionary<string, string> errors)
        {
            var st = AttendanceStatuses.Normalize(status);
            if (st == null) { errors.TryAdd(prefix + "status", _L["ValAttendanceStatus"]); return (null, null, null); }
            if (!AttendanceStatuses.Worked(st)) return (st, null, null);

            TimeSpan? inT = null, outT = null;
            if (string.IsNullOrWhiteSpace(inTime)) errors.TryAdd(prefix + "in_time", _L["ValInTimeRequired"]);
            else if (TryParseTime(inTime, out var a)) inT = a;
            else errors.TryAdd(prefix + "in_time", _L["ValTime"]);

            if (!string.IsNullOrWhiteSpace(outTime))
            {
                if (TryParseTime(outTime, out var b)) outT = b;
                else errors.TryAdd(prefix + "out_time", _L["ValTime"]);
            }
            if (inT != null && outT != null && outT <= inT) errors.TryAdd(prefix + "out_time", _L["ValOutBeforeIn"]);
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
            var today = DateTime.Today;
            start = new DateTime(today.Year, today.Month, 1);
            end = start.AddMonths(1).AddDays(-1);

            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                DateTime f = default, t = default;
                if (!string.IsNullOrWhiteSpace(from) && !TryParseDate(from, out f)) errors["from"] = _L["ValDateInvalid"];
                if (!string.IsNullOrWhiteSpace(to) && !TryParseDate(to, out t)) errors["to"] = _L["ValDateInvalid"];
                if (errors.Count > 0) return errors;
                start = string.IsNullOrWhiteSpace(from) ? t : f;
                end = string.IsNullOrWhiteSpace(to) ? f : t;
                if (start > end) errors["to"] = _L["ValDateRange"];
            }
            else if (!string.IsNullOrWhiteSpace(month))
            {
                if (DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m))
                {
                    start = m;
                    end = m.AddMonths(1).AddDays(-1);
                }
                else errors["month"] = _L["ValDateInvalid"];
            }
            return errors;
        }

        private Dictionary<string, string> BindingErrors()
        {
            var errors = ModelStateErrors(ModelState);
            // a malformed JSON body is reported under "items" by the shared key mapping
            if (errors.Remove("items", out var bodyError)) errors.TryAdd("entries", bodyError);
            return errors;
        }

        private static object RecordDto(AttendanceRecord r) => new
        {
            id = r.Id.ToString(),
            status = r.Status,
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

        internal static string? Time(TimeSpan? t) => t?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        private static string Hours(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
