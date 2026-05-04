using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStickers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StickerId",
                table: "InspectionCertificates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stickers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StickerNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AllocatedToJobOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedToCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedToEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplacedByStickerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stickers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionCertificates_StickerId",
                table: "InspectionCertificates",
                column: "StickerId",
                unique: true,
                filter: "[StickerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_IssuedToCertificateId",
                table: "Stickers",
                column: "IssuedToCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_IssuedToEquipmentId",
                table: "Stickers",
                column: "IssuedToEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_State",
                table: "Stickers",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_StickerNo",
                table: "Stickers",
                column: "StickerNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stickers");

            migrationBuilder.DropIndex(
                name: "IX_InspectionCertificates_StickerId",
                table: "InspectionCertificates");

            migrationBuilder.DropColumn(
                name: "StickerId",
                table: "InspectionCertificates");
        }
    }
}
