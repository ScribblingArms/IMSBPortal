using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <summary>Offers two more shifts in the staff form, after the existing five.</summary>
    public partial class AddTenAmShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @new TABLE (DisplayOrder int, Name nvarchar(50));
                INSERT INTO @new VALUES (6, N'10:00 AM to 05:00 PM'), (7, N'10:00 AM to 06:00 PM');
                UPDATE s SET s.DisplayOrder = n.DisplayOrder, s.Name = n.Name FROM [Shift] s JOIN @new n ON s.Code = n.Name;
                INSERT INTO [Shift] (Id, Code, Name, DisplayOrder)
                    SELECT NEWID(), n.Name, n.Name, n.DisplayOrder FROM @new n
                    WHERE NOT EXISTS (SELECT 1 FROM [Shift] s WHERE s.Code = n.Name);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // staff already on these keep the text, which the staff form tolerates as an older value
            migrationBuilder.Sql(@"DELETE FROM [Shift] WHERE Code IN (N'10:00 AM to 05:00 PM', N'10:00 AM to 06:00 PM');");
        }
    }
}
