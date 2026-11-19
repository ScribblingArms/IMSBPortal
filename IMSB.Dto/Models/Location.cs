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

    // The Mandals table (and its Mandal entity) was dropped by the RemoveMandalsAddEmployeeId migration: the stall
    // application keeps the mandal as text (SHGDetails.Mandal, ShgMandalAsText migration), so nothing read it any more.
}
