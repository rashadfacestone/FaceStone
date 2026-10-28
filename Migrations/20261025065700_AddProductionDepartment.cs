using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    StorageLocationName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "DRAFT"),
                    CurrentStage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "الخلط الأولي"),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileSize = table.Column<long>(type: "bigint", nullable: false),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    IsStorageApproved = table.Column<bool>(type: "bit", nullable: false),
                    StorageApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StorageApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOrders_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionReworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReworkNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    OriginalProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    OriginalProductionOutputId = table.Column<int>(type: "int", nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    NewProductionOrderId = table.Column<int>(type: "int", nullable: true),
                    NewProductionBatchId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionReworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionSequences",
                columns: table => new
                {
                    SequenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NextValue = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSequences", x => x.SequenceType);
                });

            migrationBuilder.CreateTable(
                name: "ProductionBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    ProductionWorkInProcessId = table.Column<int>(type: "int", nullable: true),
                    CurrentStage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "IN_PROGRESS"),
                    QualityHold = table.Column<bool>(type: "bit", nullable: false),
                    QualityHoldReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QualityHoldReportId = table.Column<int>(type: "int", nullable: true),
                    QualityHoldAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityHoldReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityHoldReleasedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalInputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalOutputQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalWasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionBatches_ProductionWorkInProcess_ProductionWorkInProcessId",
                        column: x => x.ProductionWorkInProcessId,
                        principalTable: "ProductionWorkInProcess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IssuedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    StorageLocationId = table.Column<int>(type: "int", nullable: true),
                    LaboratoryRawMaterialReportId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderItems_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionOrderItems_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOutputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutputNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    ProductionBatchId = table.Column<int>(type: "int", nullable: false),
                    ProductionReferenceNumber = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "طن"),
                    ProductionStage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OutputDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextStage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestinationStorageLocationId = table.Column<int>(type: "int", nullable: true),
                    DestinationStorageLocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LaboratoryConformityStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING_INSPECTION"),
                    LaboratoryCertificateId = table.Column<int>(type: "int", nullable: true),
                    IsStorageReceived = table.Column<bool>(type: "bit", nullable: false),
                    StorageReceivedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StorageReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StorageReceivedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    NonConformityAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReworkCreated = table.Column<bool>(type: "bit", nullable: false),
                    ReworkOrderId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentFileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_LaboratoryFinalConformityCertificates_LaboratoryCertificateId",
                        column: x => x.LaboratoryCertificateId,
                        principalTable: "LaboratoryFinalConformityCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_ProductionBatches_ProductionBatchId",
                        column: x => x.ProductionBatchId,
                        principalTable: "ProductionBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_StorageLocations_DestinationStorageLocationId",
                        column: x => x.DestinationStorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionOutputs_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_BatchNumber",
                table: "ProductionBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_ProductionOrderId",
                table: "ProductionBatches",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_ProductionReferenceNumber",
                table: "ProductionBatches",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_ProductionWorkInProcessId",
                table: "ProductionBatches",
                column: "ProductionWorkInProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_QualityHold",
                table: "ProductionBatches",
                column: "QualityHold");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatches_Status",
                table: "ProductionBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_ProductionOrderId",
                table: "ProductionOrderItems",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderItems_RawMaterialId",
                table: "ProductionOrderItems",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CreatedAt",
                table: "ProductionOrders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_OrderNumber",
                table: "ProductionOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ProductionReferenceNumber",
                table: "ProductionOrders",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_Status",
                table: "ProductionOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_StorageLocationId",
                table: "ProductionOrders",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_StorageOperationId",
                table: "ProductionOrders",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_Classification",
                table: "ProductionOutputs",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_DestinationStorageLocationId",
                table: "ProductionOutputs",
                column: "DestinationStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_IsStorageReceived",
                table: "ProductionOutputs",
                column: "IsStorageReceived");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_LaboratoryCertificateId",
                table: "ProductionOutputs",
                column: "LaboratoryCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_LaboratoryConformityStatus",
                table: "ProductionOutputs",
                column: "LaboratoryConformityStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_OutputNumber",
                table: "ProductionOutputs",
                column: "OutputNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ProductionBatchId",
                table: "ProductionOutputs",
                column: "ProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ProductionOrderId",
                table: "ProductionOutputs",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_ProductionReferenceNumber",
                table: "ProductionOutputs",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOutputs_StorageOperationId",
                table: "ProductionOutputs",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_OriginalProductionBatchId",
                table: "ProductionReworks",
                column: "OriginalProductionBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_OriginalProductionOrderId",
                table: "ProductionReworks",
                column: "OriginalProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_OriginalProductionOutputId",
                table: "ProductionReworks",
                column: "OriginalProductionOutputId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_ProductionReferenceNumber",
                table: "ProductionReworks",
                column: "ProductionReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_ReworkNumber",
                table: "ProductionReworks",
                column: "ReworkNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionReworks_Status",
                table: "ProductionReworks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionOrderItems");

            migrationBuilder.DropTable(
                name: "ProductionOutputs");

            migrationBuilder.DropTable(
                name: "ProductionReworks");

            migrationBuilder.DropTable(
                name: "ProductionSequences");

            migrationBuilder.DropTable(
                name: "ProductionBatches");

            migrationBuilder.DropTable(
                name: "ProductionOrders");
        }
    }
}
