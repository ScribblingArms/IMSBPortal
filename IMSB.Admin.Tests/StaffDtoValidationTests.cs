using System.ComponentModel.DataAnnotations;
using IMSB.Dto.Models;

namespace IMSB.Admin.Tests;

public class StaffDtoValidationTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static IEnumerable<string> FailedMembers(object dto) => Validate(dto).SelectMany(r => r.MemberNames);

    [Fact]
    public void Valid_create_dto_passes()
    {
        Assert.Empty(Validate(TestHelpers.ValidCreateDto()));
    }

    [Fact]
    public void Agency_shift_and_stall_are_optional()
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.AgencyId = null;
        dto.ShiftId = null;
        dto.StallNumber = null;
        Assert.Empty(Validate(dto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Full_name_shorter_than_two_chars_fails(string name)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.FullName = name;
        Assert.Contains(nameof(CreateStaffDto.FullName), FailedMembers(dto));
    }

    [Fact]
    public void Full_name_longer_than_100_chars_fails()
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.FullName = new string('a', 101);
        Assert.Contains(nameof(CreateStaffDto.FullName), FailedMembers(dto));
    }

    // the staff category and designation are required by the server rules (StaffValidation), which let older records
    // without them be edited; the form annotations only cap the designation at tbl_Designation's 50 characters
    [Fact]
    public void Category_and_designation_are_not_demanded_by_the_annotations()
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.DepartmentId = "";
        dto.Designation = null;
        Assert.Empty(Validate(dto));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Designation_fits_the_column(int length, bool valid)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.Designation = new string('d', length);
        Assert.Equal(valid, !FailedMembers(dto).Contains(nameof(CreateStaffDto.Designation)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(99999, true)]
    [InlineData(100000, false)]
    public void Stall_number_is_within_range(int stall, bool valid)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.StallNumber = stall;
        Assert.Equal(valid, !FailedMembers(dto).Contains(nameof(CreateStaffDto.StallNumber)));
    }

    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("INACTIVE", true)]
    [InlineData(null, true)]          // omitted = keep current status
    [InlineData("ON_LEAVE", false)]   // tbl_Employee only knows active or not
    [InlineData("active", false)]     // must be the canonical upper-case code
    public void Update_status_is_restricted(string? status, bool valid)
    {
        var dto = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(), status);
        Assert.Equal(valid, !FailedMembers(dto).Contains(nameof(UpdateStaffDto.Status)));
    }

    [Fact]
    public void Remarks_fit_the_column()
    {
        Assert.Empty(Validate(TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(), remarks: new string('r', 200))));
        Assert.Contains(nameof(UpdateStaffDto.Remarks), FailedMembers(TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(), remarks: new string('r', 201))));
    }

    [Fact]
    public void Error_messages_are_resource_keys_for_localization()
    {
        var dto = TestHelpers.ValidCreateDto(mobile: "");
        var mobileError = Validate(dto).Single(r => r.MemberNames.Contains(nameof(CreateStaffDto.Mobile)));
        Assert.Equal("ValMobile", mobileError.ErrorMessage);
    }
}
