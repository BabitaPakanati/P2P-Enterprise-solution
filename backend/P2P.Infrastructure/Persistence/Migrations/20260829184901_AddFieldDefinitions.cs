using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace P2P.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "procurement_purchase_requisition",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "procurement_purchase_order",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "configuration_field_definition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    FieldKey = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SelectOptionsJson = table.Column<string>(type: "text", nullable: true),
                    DependsOnFieldKey = table.Column<string>(type: "text", nullable: true),
                    DependsOnValue = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_field_definition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuration_field_definition_EntityType_FieldKey",
                table: "configuration_field_definition",
                columns: new[] { "EntityType", "FieldKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration_field_definition");

            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "procurement_purchase_requisition");

            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "procurement_purchase_order");
        }
    }
}
