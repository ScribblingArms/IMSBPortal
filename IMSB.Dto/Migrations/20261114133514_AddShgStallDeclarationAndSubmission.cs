using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMSB.Dto.Migrations
{
    /// <inheritdoc />
    public partial class AddShgStallDeclarationAndSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationRef",
                table: "SHGDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeclarationConsent",
                table: "SHGDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeclarationSavedAt",
                table: "SHGDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeclarationTrue",
                table: "SHGDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IdProofDocument",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreviousExhibition",
                table: "SHGDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousExhibitionDetails",
                table: "SHGDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShgDocument",
                table: "SHGDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StallDuration",
                table: "SHGDetails",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StallFacilities",
                table: "SHGDetails",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StallSavedAt",
                table: "SHGDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StallStartMonth",
                table: "SHGDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "SHGDetails",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHGDetails_ApplicationRef",
                table: "SHGDetails",
                column: "ApplicationRef",
                unique: true,
                filter: "[ApplicationRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SHGDetails_ApplicationRef",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ApplicationRef",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "DeclarationConsent",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "DeclarationSavedAt",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "DeclarationTrue",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "IdProofDocument",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "PreviousExhibition",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "PreviousExhibitionDetails",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "ShgDocument",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "StallDuration",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "StallFacilities",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "StallSavedAt",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "StallStartMonth",
                table: "SHGDetails");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "SHGDetails");
        }
    }
}
