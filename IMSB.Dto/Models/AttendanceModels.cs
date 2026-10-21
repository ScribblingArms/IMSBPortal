using System.ComponentModel.DataAnnotations;

namespace IMSB.Dto.Models
{
    // ErrorMessage values are SharedResource keys, translated by the DataAnnotations localizer (Program.cs)

    /// <summary>The Attendance Entry sheet for one day: only the rows that were marked are sent.</summary>
    public class AttendanceSheetDto
    {
        [Required(ErrorMessage = "ValDate")]
        public string Date { get; set; } = "";   // yyyy-MM-dd

        public List<AttendanceEntryDto> Entries { get; set; } = new();
    }

    public class AttendanceEntryDto
    {
        [Required(ErrorMessage = "ValStaffNotFound")]
        public string EmployeeId { get; set; } = "";

        [Required(ErrorMessage = "ValAttendanceStatus")]
        public string Status { get; set; } = "";

        [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ValTime")]
        public string? InTime { get; set; }   // HH:mm

        [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ValTime")]
        public string? OutTime { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    /// <summary>Editing one saved record (the staff member and the date stay fixed).</summary>
    public class AttendanceUpdateDto
    {
        [Required(ErrorMessage = "ValAttendanceStatus")]
        public string Status { get; set; } = "";

        [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ValTime")]
        public string? InTime { get; set; }

        [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "ValTime")]
        public string? OutTime { get; set; }

        [StringLength(500, ErrorMessage = "ValRemarks")]
        public string? Remarks { get; set; }
    }

    /// <summary>Counts for a group of sheet rows (the whole day or one department).</summary>
    public record AttendanceCounts(int Total, int Present, int Absent, int Leave, int HalfDay, int NotMarked, int WithOvertime, decimal? Percent);
}
