using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSignaturePng : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignaturePng",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                maxLength: 131072,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignaturePng",
                table: "AspNetUsers");
        }
    }
}
