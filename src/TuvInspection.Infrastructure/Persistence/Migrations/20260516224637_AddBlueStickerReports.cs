using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuvInspection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueStickerReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlueStickerReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JobOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TuvJobOrderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AramcoCategoryNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OrgCode = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RpoNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CrmNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DepartmentContractor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InspectionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectionTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    PreviousStickerNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PreviousStickerIssuedBy = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AreaOfInspection = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    EquipmentIdNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EquipmentLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EquipmentType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EquipmentSerialNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NewStickerNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StickerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StickerExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Deficiencies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectiveActionsTaken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReceiverBadgeNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReceiverTelephone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InspectorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InspectorSapNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    InspectorTelephone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TechnicalReviewerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceiverSignaturePng = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectorSignaturePng = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalReviewerSignaturePng = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    ClientOtpHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientOtpExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientOtpAttempts = table.Column<int>(type: "int", nullable: false),
                    ClientOtpSentToEmail = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueStickerReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlueStickerReportTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromState = table.Column<int>(type: "int", nullable: false),
                    ToState = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueStickerReportTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueStickerReportTransitions_BlueStickerReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "BlueStickerReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReports_EquipmentId",
                table: "BlueStickerReports",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReports_JobOrderId",
                table: "BlueStickerReports",
                column: "JobOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReports_ReportNo",
                table: "BlueStickerReports",
                column: "ReportNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReports_State",
                table: "BlueStickerReports",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReports_StickerId",
                table: "BlueStickerReports",
                column: "StickerId",
                unique: true,
                filter: "[StickerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BlueStickerReportTransitions_ReportId",
                table: "BlueStickerReportTransitions",
                column: "ReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlueStickerReportTransitions");

            migrationBuilder.DropTable(
                name: "BlueStickerReports");
        }
    }
}
