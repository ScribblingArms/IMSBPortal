using IMSB.Backend.Mappers;
using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Api.Controllers
{
    /// <summary>The overtime window of each staff member.</summary>
    [Authorize(Roles = "Admin")]
    public class OtAssignmentsController : ApiControllerBase
    {
        private readonly IOtAssignmentsInterface _assignments;

        public OtAssignmentsController(IOtAssignmentsInterface assignments) => _assignments = assignments;

        /// <summary>Every assignment, and the staff who can still be given one.</summary>
        [HttpGet]
        public async Task<IActionResult> List() =>
            Ok(new
            {
                rows = await _assignments.ListAsync(),
                staff = await _assignments.UnassignedStaffAsync(),
                today = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            });

        /// <summary>
        /// OT can be assigned from today to the last day of this month, only between the end of the staff member's shift
        /// and the start of their next one; past assignments cannot be edited.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OtAssignmentDto dto)
        {
            if (dto.ValidateTimes(DateTime.Today) is { } invalid) return Invalid(invalid.Field, invalid.Key);
            if (dto.StaffId is null || dto.StaffId == Guid.Empty) return Invalid("staff_id", "ValOtSelectStaff");

            var (from, to) = dto.Times();
            var added = await _assignments.AddAsync(dto.StaffId.Value, from, to, UserName);
            return FromResult(added, OtAssignmentMapper.ErrorField(added.ErrorKey ?? string.Empty, "staff_id"),
                () => Ok(new { message = L["OTAssignmentSaved"].Value }));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] OtAssignmentDto dto)
        {
            if (dto.ValidateTimes(DateTime.Today) is { } invalid) return Invalid(invalid.Field, invalid.Key);

            var (from, to) = dto.Times();
            var updated = await _assignments.UpdateAsync(id, from, to, UserName);
            return FromResult(updated, OtAssignmentMapper.ErrorField(updated.ErrorKey ?? string.Empty, "request"),
                () => Ok(new { message = L["OTAssignmentSaved"].Value }));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id) =>
            FromResult(await _assignments.DeleteAsync(id), "request", () => Ok(new { message = L["OTAssignmentDeleted"].Value }));
    }
}
