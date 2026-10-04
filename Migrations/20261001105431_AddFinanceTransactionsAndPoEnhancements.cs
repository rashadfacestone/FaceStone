using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceTransactionsAndPoEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PO",
                table: "Transportation");

            migrationBuilder.RenameColumn(
                name: "WeightWhenExitFactory",
                table: "Transportation",
                newName: "WeightWhenExitingFactory");

            migrationBuilder.RenameColumn(
                name: "WeightWhenEnterFactory",
                table: "Transportation",
                newName: "WeightWhenEnteringFactory");

            migrationBuilder.RenameColumn(
                name: "ShippingStatus",
                table: "Transportation",
                newName: "TransactionStatus");

            migrationBuilder.RenameColumn(
                name: "DeliveryStatus",
                table: "Transportation",
                newName: "ReceivingStatus");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Transportation",
                newName: "TransactionDate");

            migrationBuilder.RenameColumn(
                name: "CarPlatesNumber",
                table: "Transportation",
                newName: "CarNumber");

            migrationBuilder.AlterColumn<int>(
                name: "TransactionNumber",
                table: "Transportation",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfSubmit",
                table: "Transportation",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedWeight",
                table: "Transportation",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PONumber",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PricePerTon = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "Cairo Factory"),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MovementNumber = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<int>(type: "int", nullable: false),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IncomingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OutgoingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DestinationStorageLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivingEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Bank Transfer"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaidBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancePayments_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FinanceTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WeightBeforeLoading = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FactoryExitWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EntryWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ExpectedWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ActualWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Ikramiya = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    InvoiceAttachmentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InvoiceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    InvoiceFileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InvoiceFileSize = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceTransactions_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StorageTransactionMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageTransactionId = table.Column<int>(type: "int", nullable: false),
                    TransportationMovementNumber = table.Column<int>(type: "int", nullable: false),
                    TransportationId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageTransactionMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageTransactionMovements_StorageTransactions_StorageTransactionId",
                        column: x => x.StorageTransactionId,
                        principalTable: "StorageTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StorageTransactionMovements_Transportation_TransportationId",
                        column: x => x.TransportationId,
                        principalTable: "Transportation",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_PaymentDate",
                table: "FinancePayments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_PONumber",
                table: "FinancePayments",
                column: "PONumber");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePayments_PurchaseOrderId",
                table: "FinancePayments",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_PONumber",
                table: "FinanceTransactions",
                column: "PONumber");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_PurchaseOrderId",
                table: "FinanceTransactions",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_SubmissionDate",
                table: "FinanceTransactions",
                column: "SubmissionDate");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_SubmittedBy",
                table: "FinanceTransactions",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceTransactions_TransactionNumber",
                table: "FinanceTransactions",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_InvoiceNumber",
                table: "PurchaseOrders",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderDate",
                table: "PurchaseOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PONumber",
                table: "PurchaseOrders",
                column: "PONumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Supplier",
                table: "PurchaseOrders",
                column: "Supplier");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactionMovements_StorageTransactionId",
                table: "StorageTransactionMovements",
                column: "StorageTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactionMovements_TransportationId",
                table: "StorageTransactionMovements",
                column: "TransportationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactionMovements_TransportationMovementNumber",
                table: "StorageTransactionMovements",
                column: "TransportationMovementNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_MovementNumber",
                table: "StorageTransactions",
                column: "MovementNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_ReferenceNumber",
                table: "StorageTransactions",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_StorageLocation",
                table: "StorageTransactions",
                column: "StorageLocation");

            migrationBuilder.CreateIndex(
                name: "IX_StorageTransactions_TransactionDate",
                table: "StorageTransactions",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancePayments");

            migrationBuilder.DropTable(
                name: "FinanceTransactions");

            migrationBuilder.DropTable(
                name: "StorageTransactionMovements");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "StorageTransactions");

            migrationBuilder.DropColumn(
                name: "DateOfSubmit",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "ExpectedWeight",
                table: "Transportation");

            migrationBuilder.DropColumn(
                name: "PONumber",
                table: "Transportation");

            migrationBuilder.RenameColumn(
                name: "WeightWhenExitingFactory",
                table: "Transportation",
                newName: "WeightWhenExitFactory");

            migrationBuilder.RenameColumn(
                name: "WeightWhenEnteringFactory",
                table: "Transportation",
                newName: "WeightWhenEnterFactory");

            migrationBuilder.RenameColumn(
                name: "TransactionStatus",
                table: "Transportation",
                newName: "ShippingStatus");

            migrationBuilder.RenameColumn(
                name: "TransactionDate",
                table: "Transportation",
                newName: "Date");

            migrationBuilder.RenameColumn(
                name: "ReceivingStatus",
                table: "Transportation",
                newName: "DeliveryStatus");

            migrationBuilder.RenameColumn(
                name: "CarNumber",
                table: "Transportation",
                newName: "CarPlatesNumber");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionNumber",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "PO",
                table: "Transportation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
