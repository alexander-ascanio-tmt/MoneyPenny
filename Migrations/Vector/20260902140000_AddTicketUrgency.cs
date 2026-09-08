using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260902140000_AddTicketUrgency")]
    public partial class AddTicketUrgency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "urgency_detection_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PromptText = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_urgency_detection_config", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "urgency_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Phrase = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_urgency_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_urgency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    TicketNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    ClassifierVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_urgency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_urgency_TicketId",
                table: "ticket_urgency",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_urgency_rules_SortOrder",
                table: "urgency_rules",
                column: "SortOrder");

            var seededAt = new DateTime(2026, 9, 2, 14, 0, 0, DateTimeKind.Utc);
            migrationBuilder.Sql(
                $"""
                INSERT INTO urgency_detection_config ("Id", "PromptText", "UpdatedAt")
                VALUES (1,
                'Marca como URGENTE si el cliente no puede operar (facturar, cobrar, acceder al ERP), hay bloqueo total, pérdida de datos o parada de producción.
                Marca como NORMAL si es consulta general, mejora, documentación o error sin impacto operativo inmediato.
                Si hay duda, elige NORMAL.',
                TIMESTAMPTZ '{seededAt:O}');

                INSERT INTO urgency_rules ("Phrase", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                ('urgente', 10, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                ('no puedo trabajar', 20, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                ('no puedo facturar', 30, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                ('bloqueado', 40, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                ('parada de producción', 50, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL),
                ('no puedo acceder', 60, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ticket_urgency");
            migrationBuilder.DropTable(name: "urgency_rules");
            migrationBuilder.DropTable(name: "urgency_detection_config");
        }
    }
}
