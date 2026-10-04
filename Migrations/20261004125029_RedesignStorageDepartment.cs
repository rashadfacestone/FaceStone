using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class RedesignStorageDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_ReferenceNumber",
                table: "StorageTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "StorageLocation",
                table: "StorageTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "مستودع الخامات الرئيسي",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ServiceType",
                table: "StorageTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "إيداع",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "StorageTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutgoingQuantity",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "OpeningBalance",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "IncomingQuantity",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ClosingBalance",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "StorageTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Import");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "StorageTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalEmptyWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalEmptyWeightTime",
                table: "StorageTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalLoadedWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalLoadedWeightTime",
                table: "StorageTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalNetWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalWeighbridgeReference",
                table: "StorageTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasExternalWeighbridge",
                table: "StorageTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalEmptyWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalEmptyWeightTime",
                table: "StorageTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalLoadedWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalLoadedWeightTime",
                table: "StorageTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalNetWeight",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InternalWeighbridgeReference",
                table: "StorageTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsExportRelatedToPO",
                table: "StorageTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Material",
                table: "StorageTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "powder");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderId",
                table: "StorageTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeAttachmentPath",
                table: "StorageTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeFileName",
                table: "StorageTransactions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WeighbridgeFileSize",
                table: "StorageTransactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeFileType",
                table: "StorageTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_Action",
                table: "StorageTransactions",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_Material",
                table: "StorageTransactions",
                column: "Material");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_PONumber",
                table: "StorageTransactions",
                column: "PONumber");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_PurchaseOrderId",
                table: "StorageTransactions",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_ReferenceNumber",
                table: "StorageTransactions",
                column: "ReferenceNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageTransactions_PurchaseOrders_PurchaseOrderId",
                table: "StorageTransactions",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageTransactions_PurchaseOrders_PurchaseOrderId",
                table: "StorageTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_Action",
                table: "StorageTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_Material",
                table: "StorageTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_PONumber",
                table: "StorageTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_PurchaseOrderId",
                table: "StorageTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StorageTransactions_ReferenceNumber",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalEmptyWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalEmptyWeightTime",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalLoadedWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalLoadedWeightTime",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalNetWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalWeighbridgeReference",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "HasExternalWeighbridge",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalEmptyWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalEmptyWeightTime",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalLoadedWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalLoadedWeightTime",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalNetWeight",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "InternalWeighbridgeReference",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "IsExportRelatedToPO",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "Material",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "WeighbridgeAttachmentPath",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileName",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileSize",
                table: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileType",
                table: "StorageTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "StorageLocation",
                table: "StorageTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldDefaultValue: "مستودع الخامات الرئيسي");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceType",
                table: "StorageTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "إيداع");

            migrationBuilder.AlterColumn<string>(
                name: "PONumber",
                table: "StorageTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutgoingQuantity",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "OpeningBalance",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "IncomingQuantity",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "ClosingBalance",
                table: "StorageTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldDefaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_ReferenceNumber",
                table: "StorageTransactions",
                column: "ReferenceNumber",
                unique: true);
        }
    }
}
