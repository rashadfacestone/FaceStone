using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransportationDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Transportation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentSupervisor",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ازهر محمد");

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartureTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverPhone",
                table: "Transportation",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalEmptyWeight",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalEmptyWeightTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalLoadedWeight",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalLoadedWeightTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExternalNetWeight",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ExternalWeighbridgeCode",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalEmptyWeightTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalLoadedWeightTime",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalWeighbridgeCode",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalWeight",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate",
                table: "Transportation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "Transportation",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientTitle",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShippingDurationMinutes",
                table: "Transportation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingSupervisor",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "محمد ناجح");

            migrationBuilder.AddColumn<decimal>(
                name: "WasteQuantity",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DepartmentSupervisor",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DepartureTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DriverPhone",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalEmptyWeight",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalEmptyWeightTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalLoadedWeight",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalLoadedWeightTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalNetWeight",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExternalWeighbridgeCode",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "InternalEmptyWeightTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "InternalLoadedWeightTime",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "InternalWeighbridgeCode",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "InternalWeight",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "RecipientTitle",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ShippingDurationMinutes",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ShippingSupervisor",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "WasteQuantity",
                table: "Transportation");
        }
    }
}
