using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

public class StaffModelTests
{
    [Fact]
    public void New_staff_has_sensible_defaults()
    {
        var before = DateTime.UtcNow;
        var s = new Staff();

        Assert.NotEqual(Guid.Empty, s.Id);
        Assert.Equal("ACTIVE", s.Status);
        Assert.Equal("Female", s.Gender);
        Assert.Equal(string.Empty, s.EmployeeId);
        Assert.InRange(s.CreatedAt, before, DateTime.UtcNow);
        Assert.Null(s.UpdatedAt);
        Assert.Null(s.ContractEndDate);
    }

    [Fact]
    public void Each_new_staff_gets_a_distinct_id()
    {
        Assert.NotEqual(new Staff().Id, new Staff().Id);
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
