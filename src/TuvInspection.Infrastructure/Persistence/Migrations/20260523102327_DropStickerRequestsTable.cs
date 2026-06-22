using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropStickerRequestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StickerRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StickerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedCount = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecisionComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InspectorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickerRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StickerRequests_InspectorUserId",
                table: "StickerRequests",
                column: "InspectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StickerRequests_RequestNo",
                table: "StickerRequests",
                column: "RequestNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StickerRequests_State",
                table: "StickerRequests",
                column: "State");
        }
    }
}
