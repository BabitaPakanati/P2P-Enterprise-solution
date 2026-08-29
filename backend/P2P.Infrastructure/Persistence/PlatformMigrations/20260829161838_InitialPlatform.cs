using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace P2P.Infrastructure.Persistence.PlatformMigrations
{
    /// <inheritdoc />
    public partial class InitialPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organisations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgCode = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SchemaName = table.Column<string>(type: "text", nullable: false),
                    DeploymentTarget = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organisations", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "organisations",
                columns: new[] { "Id", "CreatedAtUtc", "DeploymentTarget", "DisplayName", "OrgCode", "SchemaName", "Status" },
                values: new object[,]
                {
                    { new Guid("8f14e45f-ceea-4d5f-8f9b-000000000001"), new DateTimeOffset(new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Acme Corporation", "acme", "org_acme", 1 },
                    { new Guid("8f14e45f-ceea-4d5f-8f9b-000000000002"), new DateTimeOffset(new DateTime(2026, 8, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0, "Globex Corporation", "globex", "org_globex", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_organisations_OrgCode",
                table: "organisations",
                column: "OrgCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organisations_SchemaName",
                table: "organisations",
                column: "SchemaName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organisations");
        }
    }
}
