using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMSB.Dto.Models
{
    /// <summary>A district of Telangana (seeded from <see cref="TelanganaLocations"/>).</summary>
    [Table("Districts")]
    public class District
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("Name")]
        [MaxLength(60)]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A mandal within a district (seeded from <see cref="TelanganaLocations"/>).</summary>
    [Table("Mandals")]
    public class Mandal
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("DistrictId")]
        public int DistrictId { get; set; }

        public District? District { get; set; }

        [Required]
        [Column("Name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
