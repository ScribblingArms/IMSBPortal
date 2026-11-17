using IMSB.Dto.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>
    /// The WebsiteUser role is gone: User Management now creates District Officers and State Admin Officers, so logins saved
    /// as WebsiteUser become District Officers. Data only; the model snapshot is unchanged.
    /// </summary>
    [DbContext(typeof(IMSBContext))]
    [Migration("20261112130000_WebsiteUsersToDistrictOfficers")]
    public partial class WebsiteUsersToDistrictOfficers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [logins] SET [Role] = N'DistrictOfficer' WHERE [Role] = N'WebsiteUser';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // which District Officers were website users is not recorded, so nothing is changed back
        }
    }
}
