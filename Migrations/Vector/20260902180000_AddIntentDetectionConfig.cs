using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260902180000_AddIntentDetectionConfig")]
    public partial class AddIntentDetectionConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intent_detection_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PromptText = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intent_detection_config", x => x.Id);
                });

            var seededAt = new DateTime(2026, 9, 2, 18, 0, 0, DateTimeKind.Utc);
            var promptText = """
                Clasifica la intención del comentario #1 del ticket usando uno de estos códigos:

                - howto: el cliente solicita instrucciones, pasos, documentación o cómo realizar una acción.
                - error: reporta un fallo, mensaje de error o comportamiento incorrecto del sistema.

                Si no encaja claramente en howto ni en error, responde otro.

                Responde en JSON: {"intentCode":"howto|error|otro","reason":"breve motivo","confidence":0.0-1.0}
                """.Replace("'", "''");

            migrationBuilder.Sql(
                $"""
                INSERT INTO intent_detection_config ("Id", "PromptText", "UpdatedAt")
                VALUES (1, '{promptText}', TIMESTAMPTZ '{seededAt:O}');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "intent_detection_config");
        }
    }
}
