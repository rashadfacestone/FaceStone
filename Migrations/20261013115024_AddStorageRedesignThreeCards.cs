using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceStone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageRedesignThreeCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageSequences",
                columns: table => new
                {
                    SequenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NextValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageSequences", x => x.SequenceType);
                });

            migrationBuilder.CreateTable(
                name: "StorageClientDestinations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageClientId = table.Column<int>(type: "int", nullable: false),
                    DestinationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageClientDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageClientDestinations_StorageClients_StorageClientId",
                        column: x => x.StorageClientId,
                        principalTable: "StorageClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorageOperations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionNumber = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    StorageLocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourcePhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestinationId = table.Column<int>(type: "int", nullable: true),
                    DestinationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestinationDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsibleEmployee = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsInventoryPosted = table.Column<bool>(type: "bit", nullable: false),
                    InventoryPostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageClientDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "StorageClientDestinations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "StorageClients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StorageOperations_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StorageOperationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageOperationId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageOperationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageOperationItems_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperationItems_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionWorkInProcess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageOperationId = table.Column<int>(type: "int", nullable: false),
                    StorageOperationItemId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaterialQuantityIssued = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceStorageLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "قيد التشغيل"),
                    FinalProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalProductQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WasteQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WastePercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionWorkInProcess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionWorkInProcess_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionWorkInProcess_StorageOperationItems_StorageOperationItemId",
                        column: x => x.StorageOperationItemId,
                        principalTable: "StorageOperationItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductionWorkInProcess_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StorageInventoryLedger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageLocationId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    StorageOperationId = table.Column<int>(type: "int", nullable: true),
                    StorageOperationItemId = table.Column<int>(type: "int", nullable: true),
                    MovementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RunningBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageInventoryLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageInventoryLedger_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageInventoryLedger_StorageLocations_StorageLocationId",
                        column: x => x.StorageLocationId,
                        principalTable: "StorageLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageInventoryLedger_StorageOperationItems_StorageOperationItemId",
                        column: x => x.StorageOperationItemId,
                        principalTable: "StorageOperationItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StorageInventoryLedger_StorageOperations_StorageOperationId",
                        column: x => x.StorageOperationId,
                        principalTable: "StorageOperations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_RawMaterialId",
                table: "ProductionWorkInProcess",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_Status",
                table: "ProductionWorkInProcess",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_StorageOperationId",
                table: "ProductionWorkInProcess",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_StorageOperationItemId",
                table: "ProductionWorkInProcess",
                column: "StorageOperationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionWorkInProcess_TrackingNumber",
                table: "ProductionWorkInProcess",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageClientDestinations_StorageClientId",
                table: "StorageClientDestinations",
                column: "StorageClientId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageClients_Name",
                table: "StorageClients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_MovementDate",
                table: "StorageInventoryLedger",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_RawMaterialId",
                table: "StorageInventoryLedger",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_StorageLocationId_RawMaterialId",
                table: "StorageInventoryLedger",
                columns: new[] { "StorageLocationId", "RawMaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_StorageOperationId",
                table: "StorageInventoryLedger",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventoryLedger_StorageOperationItemId",
                table: "StorageInventoryLedger",
                column: "StorageOperationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_Name",
                table: "StorageLocations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_RawMaterialId",
                table: "StorageOperationItems",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationItems_StorageOperationId",
                table: "StorageOperationItems",
                column: "StorageOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ApprovalStatus",
                table: "StorageOperations",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_ClientId",
                table: "StorageOperations",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_DestinationId",
                table: "StorageOperations",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_OperationType_TransactionNumber",
                table: "StorageOperations",
                columns: new[] { "OperationType", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_StorageLocationId",
                table: "StorageOperations",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperations_TransactionDate",
                table: "StorageOperations",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionWorkInProcess");

            migrationBuilder.DropTable(
                name: "StorageInventoryLedger");

            migrationBuilder.DropTable(
                name: "StorageSequences");

            migrationBuilder.DropTable(
                name: "StorageOperationItems");

            migrationBuilder.DropTable(
                name: "StorageOperations");

            migrationBuilder.DropTable(
                name: "StorageClientDestinations");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropTable(
                name: "StorageClients");
        }
    }
}
