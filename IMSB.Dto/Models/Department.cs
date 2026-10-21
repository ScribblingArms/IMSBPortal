using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    [Table("departments")]
    public class Department
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
