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
    public void Email_is_optional()
    {
        Assert.Empty(Validate(TestHelpers.ValidCreateDto(email: null)));
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

    [Theory]
    [InlineData("12345678")]       // 8 digits
    [InlineData("1234567890123")]  // 13 digits
    [InlineData("98765abcde")]
    [InlineData("+919876543210")]
    [InlineData("")]
    public void Mobile_must_be_9_to_12_digits(string mobile)
    {
        var dto = TestHelpers.ValidCreateDto(mobile: mobile);
        Assert.Contains(nameof(CreateStaffDto.Mobile), FailedMembers(dto));
    }

    [Theory]
    [InlineData("987654321")]
    [InlineData("987654321012")]
    public void Mobile_boundaries_are_accepted(string mobile)
    {
        Assert.Empty(Validate(TestHelpers.ValidCreateDto(mobile: mobile)));
    }

    [Fact]
    public void Invalid_email_fails()
    {
        Assert.Contains(nameof(CreateStaffDto.Email), FailedMembers(TestHelpers.ValidCreateDto(email: "not-an-email")));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1000000000)]
    public void Monthly_salary_must_be_within_supported_range(decimal salary)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.MonthlySalary = salary;
        Assert.Contains(nameof(CreateStaffDto.MonthlySalary), FailedMembers(dto));
    }

    [Theory]
    [InlineData(nameof(CreateStaffDto.DateOfBirth))]
    [InlineData(nameof(CreateStaffDto.DepartmentId))]
    [InlineData(nameof(CreateStaffDto.Designation))]
    [InlineData(nameof(CreateStaffDto.StaffCategory))]
    [InlineData(nameof(CreateStaffDto.JoinDate))]
    public void Required_fields_fail_when_empty(string property)
    {
        var dto = TestHelpers.ValidCreateDto();
        typeof(CreateStaffDto).GetProperty(property)!.SetValue(dto, "");
        Assert.Contains(property, FailedMembers(dto));
    }

    [Theory]
    [InlineData("Female", true)]
    [InlineData("Male", true)]
    [InlineData("Other", true)]
    [InlineData("Unknown", false)]
    public void Gender_is_restricted(string gender, bool valid)
    {
        var dto = TestHelpers.ValidCreateDto();
        dto.Gender = gender;
        Assert.Equal(valid, !FailedMembers(dto).Contains(nameof(CreateStaffDto.Gender)));
    }

    [Theory]
    [InlineData("ACTIVE", true)]
    [InlineData("ON_LEAVE", true)]
    [InlineData("INACTIVE", true)]
    [InlineData(null, true)]          // omitted = keep current status
    [InlineData("RETIRED", false)]
    [InlineData("active", false)]     // must be the canonical upper-case code
    public void Update_status_is_restricted(string? status, bool valid)
    {
        var dto = TestHelpers.ToUpdate(TestHelpers.ValidCreateDto(), status);
        Assert.Equal(valid, !FailedMembers(dto).Contains(nameof(UpdateStaffDto.Status)));
    }

    [Fact]
    public void Error_messages_are_resource_keys_for_localization()
    {
        var dto = TestHelpers.ValidCreateDto(mobile: "12");
        var mobileError = Validate(dto).Single(r => r.MemberNames.Contains(nameof(CreateStaffDto.Mobile)));
        Assert.Equal("ValMobile", mobileError.ErrorMessage);
    }
}
