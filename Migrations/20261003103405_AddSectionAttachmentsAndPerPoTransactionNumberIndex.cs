using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionAttachmentsAndPerPoTransactionNumberIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transportation_TransactionNumber",
                table: "Transportation");

            migrationBuilder.AddColumn<string>(
                name: "BillOfLadingAttachmentPath",
                table: "Transportation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillOfLadingFileName",
                table: "Transportation",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BillOfLadingFileSize",
                table: "Transportation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "BillOfLadingFileType",
                table: "Transportation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DailySupplyAttachmentPath",
                table: "Transportation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DailySupplyFileName",
                table: "Transportation",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DailySupplyFileSize",
                table: "Transportation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "DailySupplyFileType",
                table: "Transportation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeAttachmentPath",
                table: "Transportation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeFileName",
                table: "Transportation",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WeighbridgeFileSize",
                table: "Transportation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeFileType",
                table: "Transportation",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_PONumber_TransactionNumber",
                table: "Transportation",
                columns: new[] { "PONumber", "TransactionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transportation_PONumber_TransactionNumber",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "BillOfLadingAttachmentPath",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "BillOfLadingFileName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "BillOfLadingFileSize",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "BillOfLadingFileType",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DailySupplyAttachmentPath",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DailySupplyFileName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DailySupplyFileSize",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "DailySupplyFileType",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "WeighbridgeAttachmentPath",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileName",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileSize",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "WeighbridgeFileType",
                table: "Transportation");

            migrationBuilder.CreateIndex(
                name: "IX_Transportation_TransactionNumber",
                table: "Transportation",
                column: "TransactionNumber",
                unique: true);
        }
    }
}
