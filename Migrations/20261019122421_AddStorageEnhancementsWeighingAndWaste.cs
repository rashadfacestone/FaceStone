using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageEnhancementsWeighingAndWaste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscrepancyExplanation",
                table: "StorageOperations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalReportedWeight",
                table: "StorageOperations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalEmptyWeight",
                table: "StorageOperations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalLoadedWeight",
                table: "StorageOperations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalNetWeight",
                table: "StorageOperations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalWeighingTime",
                table: "StorageOperations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscrepancyFlagged",
                table: "StorageOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaterialType",
                table: "StorageOperations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransportationId",
                table: "StorageOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeReference",
                table: "StorageOperations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighingResponsibleEmployee",
                table: "StorageOperations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightDifference",
                table: "StorageOperations",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightDifferencePercentage",
                table: "StorageOperations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialType",
                table: "RawMaterials",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MECHANICAL");

            migrationBuilder.AddColumn<string>(
                name: "ClassificationHistory",
                table: "ProductionWorkInProcess",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DestinationStorageLocationId",
                table: "ProductionWorkInProcess",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationStorageLocationName",
                table: "ProductionWorkInProcess",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecoveredStockPosted",
                table: "ProductionWorkInProcess",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OutputResponsibleEmployee",
                table: "ProductionWorkInProcess",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoverableMaterialId",
                table: "ProductionWorkInProcess",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoverableProductName",
                table: "ProductionWorkInProcess",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecoverableQuantity",
                table: "ProductionWorkInProcess",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoverableUnit",
                table: "ProductionWorkInProcess",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "طن");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecoveredStockPostedAt",
                table: "ProductionWorkInProcess",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WasteClassification",
                table: "ProductionWorkInProcess",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ORDINARY_WASTE");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_TransportationId",
                table: "StorageOperations",
                column: "TransportationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_DestinationStorageLocationId",
                table: "ProductionWorkInProcess",
                column: "DestinationStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_RecoverableMaterialId",
                table: "ProductionWorkInProcess",
                column: "RecoverableMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionWorkInProcess_RawMaterials_RecoverableMaterialId",
                table: "ProductionWorkInProcess",
                column: "RecoverableMaterialId",
                principalTable: "RawMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionWorkInProcess_StorageLocations_DestinationStorageLocationId",
                table: "ProductionWorkInProcess",
                column: "DestinationStorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageOperations_Transportation_TransportationId",
                table: "StorageOperations",
                column: "TransportationId",
                principalTable: "Transportation",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionWorkInProcess_RawMaterials_RecoverableMaterialId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionWorkInProcess_StorageLocations_DestinationStorageLocationId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropForeignKey(
                name: "FK_StorageOperations_Transportation_TransportationId",
                table: "StorageOperations");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperations_TransportationId",
                table: "StorageOperations");

            migrationBuilder.DropIndex(
                name: "IX_ProductionWorkInProcess_DestinationStorageLocationId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropIndex(
                name: "IX_ProductionWorkInProcess_RecoverableMaterialId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "DiscrepancyExplanation",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "ExternalReportedWeight",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "InternalEmptyWeight",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "InternalLoadedWeight",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "InternalNetWeight",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "InternalWeighingTime",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "IsDiscrepancyFlagged",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "MaterialType",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "TransportationId",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "WeighbridgeReference",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "WeighingResponsibleEmployee",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "WeightDifference",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "WeightDifferencePercentage",
                table: "StorageOperations");

            migrationBuilder.DropColumn(
                name: "MaterialType",
                table: "RawMaterials");

            migrationBuilder.DropColumn(
                name: "ClassificationHistory",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "DestinationStorageLocationId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "DestinationStorageLocationName",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "IsRecoveredStockPosted",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "OutputResponsibleEmployee",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "RecoverableMaterialId",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "RecoverableProductName",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "RecoverableQuantity",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "RecoverableUnit",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "RecoveredStockPostedAt",
                table: "ProductionWorkInProcess");

            migrationBuilder.DropColumn(
                name: "WasteClassification",
                table: "ProductionWorkInProcess");
        }
    }
}
