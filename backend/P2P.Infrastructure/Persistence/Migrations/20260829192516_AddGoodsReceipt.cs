using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace P2P.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receiving_goods_receipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "text", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomFieldsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_goods_receipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receiving_goods_receipt_line",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemDescription = table.Column<string>(type: "text", nullable: false),
                    Uom = table.Column<string>(type: "text", nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantityAccepted = table.Column<decimal>(type: "numeric", nullable: false),
                    QuantityRejected = table.Column<decimal>(type: "numeric", nullable: false),
                    InspectionStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receiving_goods_receipt_line", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receiving_goods_receipt_line_receiving_goods_receipt_GoodsR~",
                        column: x => x.GoodsReceiptId,
                        principalTable: "receiving_goods_receipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_goods_receipt_line_GoodsReceiptId",
                table: "receiving_goods_receipt_line",
                column: "GoodsReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receiving_goods_receipt_line");

            migrationBuilder.DropTable(
                name: "receiving_goods_receipt");
        }
    }
}
