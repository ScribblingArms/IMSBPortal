using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("Shift")]
    public class Shift
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("Code")]
        public string Code { get; set; } = string.Empty;
        [Required]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Position in the staff form's dropdown. 0 = no longer offered: staff already on it keep it, and attendance still reads its length.</summary>
        [Column("DisplayOrder")]
        public int DisplayOrder { get; set; }
    }
}
