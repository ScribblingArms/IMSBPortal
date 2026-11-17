using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// Data only (the model is unchanged, so there is no new snapshot): stalls.IsArtisan now means "held by a registered
    /// artisan" (Artisan / Stall management) and is false for a stall allotted to an SHG application or a vacant one.
    /// Rows written with the earlier meaning (true = held by an SHG) are corrected from ArtisanId.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261116090000_StallIsArtisanMeansArtisan")]
    public partial class StallIsArtisanMeansArtisan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [stalls] SET [IsArtisan] = CASE WHEN [ArtisanId] IS NOT NULL THEN 1 ELSE 0 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [stalls] SET [IsArtisan] = CASE WHEN [ShgDetailId] IS NOT NULL THEN 1 ELSE 0 END;");
        }
    }
}
