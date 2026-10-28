using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyProductionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyProductionSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubmissionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmployeeEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Shift = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShiftStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationHours = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalGrossQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NetProductionQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProductionRatePerHour = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WastePercentage = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "SUBMITTED"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductionSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyProductionDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyProductionSubmissionId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    ProductionRatePerHour = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ProductWaste = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    NetQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProductionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyProductionDetails_DailyProductionSubmissions_DailyProductionSubmissionId",
                        column: x => x.DailyProductionSubmissionId,
                        principalTable: "DailyProductionSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionDetails_DailyProductionSubmissionId",
                table: "DailyProductionDetails",
                column: "DailyProductionSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionDetails_ProductName",
                table: "DailyProductionDetails",
                column: "ProductName");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_EmployeeEmail",
                table: "DailyProductionSubmissions",
                column: "EmployeeEmail");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_ProductionDate",
                table: "DailyProductionSubmissions",
                column: "ProductionDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_ProductionReferenceNumber",
                table: "DailyProductionSubmissions",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_Shift",
                table: "DailyProductionSubmissions",
                column: "Shift");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_Status",
                table: "DailyProductionSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DailyProductionSubmissions_SubmissionNumber",
                table: "DailyProductionSubmissions",
                column: "SubmissionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyProductionDetails");

            migrationBuilder.DropTable(
                name: "DailyProductionSubmissions");
        }
    }
}
