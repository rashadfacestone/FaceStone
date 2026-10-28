using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLaboratoryDepartmentForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_ProductionReferenceNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComplyStatus",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "COMPLY");

            migrationBuilder.AddColumn<DateTime>(
                name: "LabManagerDate",
                table: "LaboratoryRawMaterialReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabManagerName",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabManagerSignature",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportIssueDate",
                table: "LaboratoryRawMaterialReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleDescription",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleName",
                table: "LaboratoryRawMaterialReports",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "سيليكا باودر (Micro Silica Powder)");

            migrationBuilder.AddColumn<int>(
                name: "SampleNumber",
                table: "LaboratoryRawMaterialReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SampleReceivedDate",
                table: "LaboratoryRawMaterialReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LaboratoryUserSampleSequences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextSampleNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryUserSampleSequences", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_BatchNumber",
                table: "LaboratoryRawMaterialReports",
                column: "BatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ComplyStatus",
                table: "LaboratoryRawMaterialReports",
                column: "ComplyStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ProductionReferenceNumber",
                table: "LaboratoryRawMaterialReports",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber",
                table: "LaboratoryRawMaterialReports",
                columns: new[] { "ResponsibleUserId", "SampleNumber" },
                unique: true,
                filter: "[ResponsibleUserId] IS NOT NULL AND [SampleNumber] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_SampleNumber",
                table: "LaboratoryRawMaterialReports",
                column: "SampleNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaboratoryUserSampleSequences");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_BatchNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_ComplyStatus",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_ProductionReferenceNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_ResponsibleUserId_SampleNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_LaboratoryRawMaterialReports_SampleNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ComplyStatus",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "LabManagerDate",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "LabManagerName",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "LabManagerSignature",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "ReportIssueDate",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleDescription",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleName",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleNumber",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.DropColumn(
                name: "SampleReceivedDate",
                table: "LaboratoryRawMaterialReports");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ProductionReferenceNumber",
                table: "LaboratoryRawMaterialReports",
                column: "ProductionReferenceNumber",
                unique: true);
        }
    }
}
