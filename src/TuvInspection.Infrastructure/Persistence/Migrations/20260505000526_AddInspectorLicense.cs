using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectorLicense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicenseAuthority",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "AspNetUsers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseScope",
                table: "AspNetUsers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseValidFrom",
                table: "AspNetUsers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseValidUntil",
                table: "AspNetUsers",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseAuthority",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenseScope",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenseValidFrom",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LicenseValidUntil",
                table: "AspNetUsers");
        }
    }
}
