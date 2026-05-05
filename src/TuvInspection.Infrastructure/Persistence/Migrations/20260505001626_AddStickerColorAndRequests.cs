using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStickerColorAndRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAtUtc",
                table: "Stickers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedFromRequestId",
                table: "Stickers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToInspectorId",
                table: "Stickers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Color",
                table: "Stickers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StickerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InspectorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Color = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AllocatedCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickerRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_AssignedToInspectorId",
                table: "Stickers",
                column: "AssignedToInspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_Color",
                table: "Stickers",
                column: "Color");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StickerRequests");

            migrationBuilder.DropIndex(
                name: "IX_Stickers_AssignedToInspectorId",
                table: "Stickers");

            migrationBuilder.DropIndex(
                name: "IX_Stickers_Color",
                table: "Stickers");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                table: "Stickers");

            migrationBuilder.DropColumn(
                name: "AssignedFromRequestId",
                table: "Stickers");

            migrationBuilder.DropColumn(
                name: "AssignedToInspectorId",
                table: "Stickers");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "Stickers");
        }
    }
}
