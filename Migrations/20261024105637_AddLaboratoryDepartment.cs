using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoryDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductionReferenceNumber",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionReferenceNumber",
                table: "StorageOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductionReferenceNumber",
                table: "ProductionWorkInProcess",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LaboratoryProductionSequences",
                columns: table => new
                {
                    SequenceName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NextValue = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryProductionSequences", x => x.SequenceName);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryRawMaterialReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    LaboratorySection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AnalysisDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawMaterialDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceTransportationId = table.Column<int>(type: "int", nullable: true),
                    TransportationTransactionNumber = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AnalysisResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectionDecision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    ReviewedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsStorageReceived = table.Column<bool>(type: "bit", nullable: false),
                    StorageReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryRawMaterialReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaboratoryRawMaterialReports_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LaboratoryRawMaterialReports_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LaboratoryRawMaterialReports_Transportation_SourceTransportationId",
                        column: x => x.SourceTransportationId,
                        principalTable: "Transportation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryFinalConformityCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    RawMaterialReportId = table.Column<int>(type: "int", nullable: true),
                    LaboratorySection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FinalProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActualFinalProductQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    ProductionBatchNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionWorkInProcessId = table.Column<int>(type: "int", nullable: true),
                    ProductionCompletionDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActualRawMaterialInputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CalculatedDifference = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualMeasuredWaste = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WasteClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "ORDINARY_WASTE"),
                    ConformityDecision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NonConformityAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClassifiedProductType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReworkInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecificationResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observations = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    ReviewedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsStoragePosted = table.Column<bool>(type: "bit", nullable: false),
                    StoragePostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryFinalConformityCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaboratoryFinalConformityCertificates_LaboratoryRawMaterialReports_RawMaterialReportId",
                        column: x => x.RawMaterialReportId,
                        principalTable: "LaboratoryRawMaterialReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LaboratoryFinalConformityCertificates_ProductionWorkInProcess_ProductionWorkInProcessId",
                        column: x => x.ProductionWorkInProcessId,
                        principalTable: "ProductionWorkInProcess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LaboratoryInProcessReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    RawMaterialReportId = table.Column<int>(type: "int", nullable: true),
                    ProductionBatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductionWorkInProcessId = table.Column<int>(type: "int", nullable: true),
                    LaboratorySection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TestDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductionStage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnalysisResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecificationCriteria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InspectionDecision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratoryInProcessReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaboratoryInProcessReports_LaboratoryRawMaterialReports_RawMaterialReportId",
                        column: x => x.RawMaterialReportId,
                        principalTable: "LaboratoryRawMaterialReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LaboratoryInProcessReports_ProductionWorkInProcess_ProductionWorkInProcessId",
                        column: x => x.ProductionWorkInProcessId,
                        principalTable: "ProductionWorkInProcess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_ProductionReferenceNumber",
                table: "Transportation",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ProductionReferenceNumber",
                table: "StorageOperations",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_ProductionReferenceNumber",
                table: "ProductionWorkInProcess",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_ApprovalStatus",
                table: "LaboratoryFinalConformityCertificates",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_CertificateNumber",
                table: "LaboratoryFinalConformityCertificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_ConformityDecision",
                table: "LaboratoryFinalConformityCertificates",
                column: "ConformityDecision");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_LaboratorySection",
                table: "LaboratoryFinalConformityCertificates",
                column: "LaboratorySection");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_ProductionReferenceNumber",
                table: "LaboratoryFinalConformityCertificates",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_ProductionWorkInProcessId",
                table: "LaboratoryFinalConformityCertificates",
                column: "ProductionWorkInProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryFinalConformityCertificates_RawMaterialReportId",
                table: "LaboratoryFinalConformityCertificates",
                column: "RawMaterialReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_LaboratorySection",
                table: "LaboratoryInProcessReports",
                column: "LaboratorySection");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_ProductionReferenceNumber",
                table: "LaboratoryInProcessReports",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_ProductionStage",
                table: "LaboratoryInProcessReports",
                column: "ProductionStage");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_ProductionWorkInProcessId",
                table: "LaboratoryInProcessReports",
                column: "ProductionWorkInProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_RawMaterialReportId",
                table: "LaboratoryInProcessReports",
                column: "RawMaterialReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryInProcessReports_TestDateTime",
                table: "LaboratoryInProcessReports",
                column: "TestDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_AnalysisDateTime",
                table: "LaboratoryRawMaterialReports",
                column: "AnalysisDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ApprovalStatus",
                table: "LaboratoryRawMaterialReports",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_InspectionDecision",
                table: "LaboratoryRawMaterialReports",
                column: "InspectionDecision");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_LaboratorySection",
                table: "LaboratoryRawMaterialReports",
                column: "LaboratorySection");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_ProductionReferenceNumber",
                table: "LaboratoryRawMaterialReports",
                column: "ProductionReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_PurchaseOrderId",
                table: "LaboratoryRawMaterialReports",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_RawMaterialId",
                table: "LaboratoryRawMaterialReports",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_LaboratoryRawMaterialReports_SourceTransportationId",
                table: "LaboratoryRawMaterialReports",
                column: "SourceTransportationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaboratoryFinalConformityCertificates");

            migrationBuilder.DropTable(
                name: "LaboratoryInProcessReports");

            migrationBuilder.DropTable(
                name: "LaboratoryProductionSequences");

            migrationBuilder.DropTable(
                name: "LaboratoryRawMaterialReports");

            migrationBuilder.DropIndex(
                name: "IX_Transportation_ProductionReferenceNumber",
                table: "Transportation");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperations_ProductionReferenceNumber",
                table: "StorageOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionWorkInProcess_ProductionReferenceNumber",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "ProductionReferenceNumber",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ProductionReferenceNumber",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ProductionReferenceNumber",
                table: "ProductionWorkInProcess");
        }
    }
}
