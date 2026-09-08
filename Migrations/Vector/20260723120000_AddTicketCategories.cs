using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260723120000_AddTicketCategories")]
    public partial class AddTicketCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_category_assignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    TicketNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    ClassifierVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_category_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_category_assignments_ticket_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ticket_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_categories_Code",
                table: "ticket_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_categories_SortOrder",
                table: "ticket_categories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_category_assignments_CategoryId",
                table: "ticket_category_assignments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_category_assignments_TicketId",
                table: "ticket_category_assignments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_category_assignments_TicketId_CategoryId",
                table: "ticket_category_assignments",
                columns: new[] { "TicketId", "CategoryId" },
                unique: true);

            var seededAt = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
            migrationBuilder.Sql(
                $"""
                INSERT INTO ticket_categories ("Id", "Code", "Name", "Description", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                (1, 'howto', 'Cómo hacer', 'El cliente solicita documentación, pasos o cómo realizar una acción.', 10, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                (2, 'error', 'Error', 'Fallo, mensaje de error o comportamiento incorrecto.', 20, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                (3, 'bloqueo_rendimiento', 'Bloqueo / Rendimiento', 'La aplicación se bloquea, congela o va lenta.', 30, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                (4, 'escalado', 'Escalado', 'Caso para derivar o escalar a otra área.', 40, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                (5, 'otro', 'Otro', 'No encaja en las categorías anteriores.', 90, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL)
                ON CONFLICT ("Id") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """SELECT setval(pg_get_serial_sequence('ticket_categories', 'Id'), (SELECT COALESCE(MAX("Id"), 1) FROM ticket_categories));""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ticket_category_assignments");
            migrationBuilder.DropTable(name: "ticket_categories");
        }
    }
}
