using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Admin.Tests;

/// <summary>
/// On SQL Server the Staff set is the view dbo.vw_StaffEmployees over SERP.tbl_Employee (EmployeesReplaceStaff migration).
/// The in-memory test database has no views, so this context does the view's work: after every save that touches
/// employees or designations, each tbl_Employee row is copied into the Staff set the way the view presents it
/// (<see cref="ToStaff"/> mirrors the view's SQL). Staff rows seeded directly by older tests are left alone.
/// </summary>
internal sealed class StaffViewTestContext : IMSBContext
{
    private bool _syncing;

    public StaffViewTestContext(DbContextOptions<IMSBContext> options) : base(options) { }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var (touched, deleted) = Pending();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        if (touched) SyncStaffView(deleted);
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var (touched, deleted) = Pending();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (touched) SyncStaffView(deleted);
        return result;
    }

    private (bool Touched, List<int> Deleted) Pending()
    {
        if (_syncing) return (false, new());
        var changes = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        var touched = changes.Any(e => e.Entity is Employee or EmployeeDesignation or Department or Shift);
        var deleted = changes.Where(e => e.State == EntityState.Deleted && e.Entity is Employee).Select(e => ((Employee)e.Entity).EmpId).ToList();
        return (touched, deleted);
    }

    private void SyncStaffView(List<int> deletedEmployeeIds)
    {
        _syncing = true;
        try
        {
            var designations = EmployeeDesignations.AsNoTracking().ToList();
            var departments = Departments.AsNoTracking().ToDictionary(d => d.Id, d => d.Name);
            var shifts = Shifts.AsNoTracking().ToDictionary(s => s.Id, s => s.Code);
            foreach (var employee in Employees.AsNoTracking().ToList())
            {
                var row = ToStaff(employee, designations, departments, shifts);
                var existing = Staff.Find(row.Id);
                if (existing == null) Staff.Add(row);
                else Entry(existing).CurrentValues.SetValues(row);
            }
            foreach (var id in deletedEmployeeIds)
                if (Staff.Find(id) is { } gone) Staff.Remove(gone);
            base.SaveChanges(true);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>One tbl_Employee row as dbo.vw_StaffEmployees presents it.</summary>
    public static Staff ToStaff(Employee e, IReadOnlyCollection<EmployeeDesignation> designations,
        IReadOnlyDictionary<int, string> departments, IReadOnlyDictionary<int, string> shifts)
    {
        // OUTER APPLY TOP (1): the employee's own row first
        var designation = designations.Where(d => d.DesigId == e.DesigId).OrderBy(d => d.EmpId == e.EmpId ? 0 : 1).FirstOrDefault();
        return new Staff
        {
            Id = e.EmpId,
            // the employee code ("EMP001"); the EMP_ID as text for a row without one (RemoveMandalsAddEmployeeId)
            EmployeeId = e.EmployeeId ?? e.EmpId.ToString(),
            FullName = e.EmpName ?? "",
            Mobile = e.Mobile ?? "",
            Email = null,
            AadhaarHash = null,
            AadhaarLast4 = e.Aadhar is { Length: >= 4 } a ? a[^4..] : null,
            DateOfBirth = new DateTime(1900, 1, 1),
            Gender = e.Gender ?? "",
            DepartmentId = e.DepartmentId ?? 0,
            EmployeeType = null,
            Designation = designation?.Name ?? "",
            StaffCategory = e.DepartmentId is int dep && departments.TryGetValue(dep, out var name) ? name : "",
            MonthlySalary = designation?.BaseSalary ?? 0m,
            JoinDate = e.JoinDate ?? e.CreateOn?.Date ?? new DateTime(1900, 1, 1),
            Shift = e.ShiftId is int s && shifts.TryGetValue(s, out var code) ? code : "",
            Status = e.IsActive == 1 ? "ACTIVE" : "INACTIVE",
            CreatedAt = e.CreateOn ?? new DateTime(1900, 1, 1),
            UpdatedAt = e.ModifiedOn,
            ContractEndDate = null
        };
    }
}
