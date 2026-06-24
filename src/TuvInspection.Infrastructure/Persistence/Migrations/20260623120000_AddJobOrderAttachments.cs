using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TuvInspection.Infrastructure.Persistence;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the JSON-backed <c>AttachmentKeys</c> column to JobOrders (Ahmed comment #2 —
    /// PDF/image attachments on a job order). Stored as nvarchar(max), defaulting to an
    /// empty JSON array for existing rows.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260623120000_AddJobOrderAttachments")]
    public partial class AddJobOrderAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentKeys",
                table: "JobOrders",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentKeys",
                table: "JobOrders");
        }
    }
}
