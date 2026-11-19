using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IMSB.Backend
{
    /// <summary>
    /// Backend of the Staff page: employees in SERP.tbl_Employee, with EmpTypeID from SERP.tbl_EMP_Types, Desig from
    /// dbo.departments (the staff category), ShiftID from dbo.Shift and AgencyID from SERP.tbl_ArtisansTypeAgency.
    /// </summary>
    public class StaffDetails : BackendBase, IStaffInterface
    {
        public StaffDetails(IMSBContext context) : base(context) { }

        public Task<List<Employee>> AllAsync() => WithoutPhotos(_context.Employees.AsNoTracking()).ToListAsync();

        public Task<List<Employee>> FilterAsync(string? q, string? departmentId, string? status, string? employeeTypeId = null) =>
            WithoutPhotos(ApplyFilters(_context.Employees.AsNoTracking(), q, departmentId, status, employeeTypeId)
                .OrderByDescending(e => e.CreateOn).ThenByDescending(e => e.EmpId)).ToListAsync();

        /// <summary>
        /// Lists never read the photo or fingerprint (each can be tens of KB per row); HasPhoto says whether there is a photo,
        /// which /Staff/Photo/{id} then serves.
        /// </summary>
        private static IQueryable<Employee> WithoutPhotos(IQueryable<Employee> query) => query.Select(e => new Employee
        {
            EmpId = e.EmpId, EmployeeId = e.EmployeeId, EmpName = e.EmpName, Aadhar = e.Aadhar, Mobile = e.Mobile, EmpTypeId = e.EmpTypeId, DesigId = e.DesigId,
            ShiftId = e.ShiftId, IsActive = e.IsActive, CreateOn = e.CreateOn, ModifiedOn = e.ModifiedOn, ImagePath = e.ImagePath,
            Gender = e.Gender, StallNum = e.StallNum, AgencyId = e.AgencyId, Remarks = e.Remarks, JoinDate = e.JoinDate,
            DepartmentId = e.DepartmentId, HasPhoto = e.ImageBase64 != null && e.ImageBase64 != ""
        });

        private static IQueryable<Employee> ApplyFilters(IQueryable<Employee> query, string? q, string? departmentId, string? status, string? employeeTypeId)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                var idTerm = int.TryParse(term, out var id) ? id : (int?)null;
                query = query.Where(e => (e.EmpName != null && e.EmpName.ToLower().Contains(term))
                    || (e.Mobile != null && e.Mobile.Contains(term))
                    || (e.EmployeeId != null && e.EmployeeId.ToLower().Contains(term))
                    || e.EmpId == idTerm);
            }
            if (!string.IsNullOrWhiteSpace(employeeTypeId) && int.TryParse(employeeTypeId, out var typeId))
            {
                query = query.Where(e => e.EmpTypeId == typeId);
            }
            if (!string.IsNullOrWhiteSpace(departmentId) && int.TryParse(departmentId, out var deptId))
            {
                query = query.Where(e => e.DepartmentId == deptId);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var wanted = status.Trim().ToUpperInvariant();
                if (wanted == EmployeeStatuses.Active) query = query.Where(e => e.IsActive == 1);
                else if (wanted == EmployeeStatuses.Inactive) query = query.Where(e => e.IsActive == null || e.IsActive != 1);
            }
            return query;
        }

        public async Task<EmployeeLookups> LookupsAsync() => new(
            await _context.EmployeeTypeMasters.AsNoTracking().OrderBy(t => t.Name).ThenBy(t => t.EmpTypeId).ToListAsync(),
            await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ThenBy(d => d.Id).ToListAsync(),
            await _context.ArtisansTypeAgencies.AsNoTracking().OrderBy(a => a.AgencyId).ToListAsync(),
            await _context.Shifts.AsNoTracking().OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name).ToListAsync(),
            await _context.EmployeeDesignations.AsNoTracking().OrderBy(d => d.Name).ThenBy(d => d.DesigId).ToListAsync());

        public Task<Employee?> FindAsync(int employeeId) => _context.Employees.FirstOrDefaultAsync(e => e.EmpId == employeeId);

        public Task<bool> EmployeeTypeExistsAsync(int employeeTypeId) => _context.EmployeeTypeMasters.AnyAsync(t => t.EmpTypeId == employeeTypeId);

        public Task<bool> DepartmentExistsAsync(int departmentId) => _context.Departments.AnyAsync(d => d.Id == departmentId);

        public Task<bool> ShiftOfferedAsync(int shiftId) => _context.Shifts.AnyAsync(s => s.Id == shiftId && s.DisplayOrder > 0);

        public Task<bool> AgencyExistsAsync(int agencyId) => _context.ArtisansTypeAgencies.AnyAsync(a => a.AgencyId == agencyId);

        public Task<bool> MobileExistsAsync(string mobile, int? exceptId) =>
            _context.Employees.AnyAsync(e => e.Mobile == mobile && e.EmpId != exceptId);

        public Task<bool> AadhaarExistsAsync(string aadhaar, int? exceptId) =>
            _context.Employees.AnyAsync(e => e.Aadhar == aadhaar && e.EmpId != exceptId);

        public Task<List<string>> LeaveTypesAsync() =>
            _context.LeaveTypes.AsNoTracking().OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();

        public Task<string?> PhotoAsync(int employeeId) =>
            _context.Employees.AsNoTracking().Where(e => e.EmpId == employeeId).Select(e => e.ImageBase64).FirstOrDefaultAsync();

        public Task AddAsync(Employee employee, IReadOnlyCollection<LeaveQuotaDto>? quotas = null, DesignationInput? designation = null) =>
            InTransactionAsync(async () =>
            {
                // the employee code is given here, once; an id the caller set is ignored
                employee.EmployeeId = await NextEmployeeIdAsync();
                // saved first: the designation row and the quotas need the new EMP_ID
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                await ApplyDesignationAsync(employee, designation);
                await AddLeaveQuotasAsync(employee, quotas);
                await _context.SaveChangesAsync();
            });

        public Task UpdateAsync(Employee employee, DesignationInput? designation = null) =>
            InTransactionAsync(async () =>
            {
                // the code never changes; an employee saved before codes existed gets one now
                if (string.IsNullOrWhiteSpace(employee.EmployeeId)) employee.EmployeeId = await NextEmployeeIdAsync();
                await ApplyDesignationAsync(employee, designation);
                await _context.SaveChangesAsync();
            });

        /// <summary>
        /// The next employee code ("EMP001", ...). SQL Server: the next number of the sequence SERP.EmployeeIdSeq, which the
        /// trigger SERP.SetEmployeeId also takes from, so codes are never repeated, even after a delete. Other providers
        /// (the in-memory test database): one more than the highest code on record.
        /// </summary>
        private async Task<string> NextEmployeeIdAsync()
        {
            if (_context.Database.IsRelational())
            {
                // a plain command: EF's SqlQueryRaw wraps its SQL in a subquery, where SQL Server refuses NEXT VALUE FOR
                await _context.Database.OpenConnectionAsync();
                try
                {
                    await using var command = _context.Database.GetDbConnection().CreateCommand();
                    command.CommandText = $"SELECT CAST(NEXT VALUE FOR [{EmployeeIds.SequenceSchema}].[{EmployeeIds.SequenceName}] AS bigint);";
                    command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                    return EmployeeIds.Format(Convert.ToInt64(await command.ExecuteScalarAsync()));
                }
                finally
                {
                    await _context.Database.CloseConnectionAsync();
                }
            }
            var codes = await _context.Employees.Where(e => e.EmployeeId != null).Select(e => e.EmployeeId).ToListAsync();
            var tracked = _context.ChangeTracker.Entries<Employee>().Select(e => e.Entity.EmployeeId);   // added, not saved yet
            return EmployeeIds.Next(codes.Concat(tracked));
        }

        /// <summary>
        /// Writes the designation to the employee's own tbl_Designation row (EmpId = EMP_ID), adding one when they have none:
        /// a row from the SERP app, shared by other employees, is never changed. tbl_Designation has no identity column, so a
        /// new row takes the next DesigID; the surrounding transaction keeps two saves from taking the same one.
        /// </summary>
        private async Task ApplyDesignationAsync(Employee employee, DesignationInput? input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Name)) return;
            var row = employee.DesigId == null ? null
                : await _context.EmployeeDesignations.FirstOrDefaultAsync(d => d.DesigId == employee.DesigId && d.EmpId == employee.EmpId);
            if (row == null)
            {
                var next = (await _context.EmployeeDesignations.MaxAsync(d => (int?)d.DesigId) ?? 0) + 1;
                row = new EmployeeDesignation { DesigId = next, EmpId = employee.EmpId };
                _context.EmployeeDesignations.Add(row);
                employee.DesigId = row.DesigId;
            }
            row.Name = input.Name.Trim();
            row.EmpTypeId = employee.EmpTypeId;
            if (input.SetSalary) row.BaseSalary = input.Salary;
        }

        /// <summary>
        /// This year's quotas of a new employee, in leave_entitlements (as the Leaves page keeps them). The Staff Category name is
        /// the entitlement's category; leave marked on attendance draws on the first type (by name) that has days.
        /// </summary>
        private async Task AddLeaveQuotasAsync(Employee employee, IReadOnlyCollection<LeaveQuotaDto>? quotas)
        {
            var given = (quotas ?? Array.Empty<LeaveQuotaDto>()).Where(q => q.AnnualQuota != null)
                .OrderBy(q => q.LeaveType.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            if (given.Count == 0) return;
            var category = await _context.Departments.AsNoTracking().Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync() ?? "";
            if (category.Length > 100) category = category[..100];
            var attendanceType = given.FirstOrDefault(q => q.AnnualQuota > 0m) ?? given[0];
            _context.LeaveEntitlements.AddRange(given.Select(q => new LeaveEntitlement
            {
                StaffId = employee.EmpId,
                StaffCategory = category,
                LeaveType = q.LeaveType.Trim(),
                Year = DateTime.Today.Year,
                AnnualQuota = q.AnnualQuota!.Value,
                IsAttendanceLeaveType = ReferenceEquals(q, attendanceType)
            }));
        }

        /// <summary>A transaction on SQL Server; the in-memory test database has none, and needs none.</summary>
        private async Task InTransactionAsync(Func<Task> work)
        {
            if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
            {
                await work();
                return;
            }
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await work();
            await tx.CommitAsync();
        }

        public async Task<bool> RemoveAsync(Employee employee)
        {
            // the database cascades from tbl_Employee to these (EmployeesReplaceStaff migration); removing them here does the
            // same where EF sees no foreign key (the Staff read model is a view)
            var id = employee.EmpId;
            _context.AttendanceRecords.RemoveRange(await _context.AttendanceRecords.Where(r => r.StaffId == id).ToListAsync());
            _context.OtAssignments.RemoveRange(await _context.OtAssignments.Where(a => a.StaffId == id).ToListAsync());
            _context.LeaveEntitlements.RemoveRange(await _context.LeaveEntitlements.Where(e => e.StaffId == id).ToListAsync());
            _context.StaffLeaveEntries.RemoveRange(await _context.StaffLeaveEntries.Where(e => e.StaffId == id).ToListAsync());
            // the employee's own designation rows go with them; shared SERP rows stay
            _context.EmployeeDesignations.RemoveRange(await _context.EmployeeDesignations.Where(d => d.EmpId == id).ToListAsync());
            _context.Employees.Remove(employee);
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // e.g. FK_tbl_EMPDailyAttendance_EMP_ID_tbl_Employee: keep the employee (and everything above) and let the page suggest Inactive
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToList())
                    entry.State = EntityState.Unchanged;
                return false;
            }
        }
    }
}
