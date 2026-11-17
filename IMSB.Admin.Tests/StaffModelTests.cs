using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

public class StaffModelTests
{
    [Fact]
    public void New_staff_has_sensible_defaults()
    {
        var before = DateTime.UtcNow;
        var s = new Staff();

        Assert.Equal(0, s.Id);   // int IDENTITY: the database assigns it when the staff member is saved
        Assert.Equal("ACTIVE", s.Status);
        Assert.Equal("Female", s.Gender);
        Assert.Equal(string.Empty, s.EmployeeId);
        Assert.InRange(s.CreatedAt, before, DateTime.UtcNow);
        Assert.Null(s.UpdatedAt);
        Assert.Null(s.ContractEndDate);
    }

    [Fact]
    public void Each_saved_staff_gets_a_distinct_id()
    {
        using var context = TestHelpers.CreateContext();
        var first = TestHelpers.ExistingStaff("9001", "First", "9000000001");
        var second = TestHelpers.ExistingStaff("9002", "Second", "9000000002");
        first.Id = second.Id = 0;   // let the store number them
        context.AddEmployees(first, second);
        context.SaveChanges();
        Assert.True(first.Id > 0 && second.Id > 0);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("ACTIVE", true, false)]
    [InlineData("ON_LEAVE", false, true)]
    [InlineData("INACTIVE", false, false)]
    public void IsActive_and_IsOnLeave_follow_status(string status, bool active, bool onLeave)
    {
        var s = new Staff { Status = status };
        Assert.Equal(active, s.IsActive);
        Assert.Equal(onLeave, s.IsOnLeave);
    }
}
