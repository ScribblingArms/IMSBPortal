using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("Shift")]
    public class Shift
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("Code")]
        public string Code { get; set; } = string.Empty;
        [Required]
        [Column("Name")]
        public string Name { get; set; } = string.Empty;
    }
}
