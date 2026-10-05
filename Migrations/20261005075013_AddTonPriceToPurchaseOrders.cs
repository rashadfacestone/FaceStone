using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTonPriceToPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TonPrice",
                table: "PurchaseOrders",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("UPDATE PurchaseOrders SET TonPrice = PricePerTon WHERE TonPrice = 0;");
            migrationBuilder.Sql("UPDATE t SET t.PurchaseOrderId = po.Id FROM Transportation t INNER JOIN PurchaseOrders po ON t.PONumber = po.PONumber WHERE t.PurchaseOrderId IS NULL;");
            migrationBuilder.Sql("UPDATE s SET s.PurchaseOrderId = po.Id FROM StorageTransactions s INNER JOIN PurchaseOrders po ON s.PONumber = po.PONumber WHERE s.PurchaseOrderId IS NULL AND s.PONumber IS NOT NULL AND s.PONumber <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TonPrice",
                table: "PurchaseOrders");
        }
    }
}
