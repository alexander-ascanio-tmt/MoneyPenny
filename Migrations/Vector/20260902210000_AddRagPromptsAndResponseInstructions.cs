using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;
using MoneyPenny.Services.Rag;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260902210000_AddRagPromptsAndResponseInstructions")]
    public partial class AddRagPromptsAndResponseInstructions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseInstructions",
                table: "ticket_intents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rag_prompt_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "text", nullable: false),
                    GenerationQuestion = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_prompt_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "urgency_profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResponseInstructions = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_urgency_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rag_prompt_selection_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsUrgent = table.Column<bool>(type: "boolean", nullable: true),
                    IntentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_prompt_selection_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_prompt_selection_rules_rag_prompt_templates_PromptTempl~",
                        column: x => x.PromptTemplateId,
                        principalTable: "rag_prompt_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rag_prompt_selection_rules_Priority",
                table: "rag_prompt_selection_rules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_rag_prompt_selection_rules_PromptTemplateId",
                table: "rag_prompt_selection_rules",
                column: "PromptTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_prompt_templates_Code",
                table: "rag_prompt_templates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rag_prompt_templates_SortOrder",
                table: "rag_prompt_templates",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_urgency_profiles_Code",
                table: "urgency_profiles",
                column: "Code",
                unique: true);

            var seededAt = new DateTime(2026, 9, 2, 21, 0, 0, DateTimeKind.Utc);
            var systemPrompt = """
                Eres un asistente de soporte técnico de Telematel. Redactas respuestas en español para enviar directamente al cliente por ticket.
                El texto debe poder copiarse y pegarse como respuesta oficial: tono profesional, cercano y claro.
                Usa únicamente la información del contexto proporcionado.
                No des instrucciones al agente de soporte ni hables en tercera persona sobre lo que "debe hacer" el equipo.
                No menciones tickets internos de referencia, números de ticket ajenos al actual ni procesos internos salvo que el cliente deba conocerlos.
                Si no hay información suficiente, indícalo al cliente de forma clara y pide amablemente los datos que falten.
                """.Replace("'", "''");

            var userPrompt = """
                Ticket actual (#{{ticketNumber}}) — comentario #1 indexado:
                {{currentTicketComment}}

                Contexto recuperado (tickets con problema similar; comentarios posteriores al #1 del cliente, sin repetir el problema inicial):
                {{context}}

                Pregunta:
                {{question}}

                Instrucciones:
                - El comentario #1 del ticket actual es lo que escribió el cliente; tu respuesta va dirigida a él o ella.
                - El contexto recuperado incluye cómo se resolvieron casos similares en comentarios posteriores (respuestas de soporte, aclaraciones y cierre).
                - Redacta un mensaje para el cliente con la solución o los pasos que debe seguir, inspirándote en esas resoluciones previas y en el problema actual.
                - Escribe en segunda persona, como en un comentario o correo de soporte al cliente; sin encabezados tipo "Para el agente" ni listas de tareas internas.
                - No inventes pasos ni datos sin apoyo en el contexto.
                - Si no hay información suficiente, explícaselo al cliente y pide amablemente lo que necesites para continuar.
                - No cites al cliente números de ticket de referencia del contexto; son uso interno.
                """.Replace("'", "''");

            var generationQuestion = RagOrchestrator.DefaultGenerationQuestion.Replace("'", "''");

            migrationBuilder.Sql(
                $"""
                INSERT INTO rag_prompt_templates ("Code", "Name", "SystemPrompt", "UserPromptTemplate", "GenerationQuestion", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES ('default', 'Plantilla por defecto', '{systemPrompt}', '{userPrompt}', '{generationQuestion}', 10, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL);

                INSERT INTO urgency_profiles ("Code", "Name", "ResponseInstructions", "UpdatedAt")
                VALUES
                ('urgent', 'Urgente', '', TIMESTAMPTZ '{seededAt:O}'),
                ('normal', 'Normal', '', TIMESTAMPTZ '{seededAt:O}');

                INSERT INTO rag_prompt_selection_rules ("PromptTemplateId", "Name", "IsUrgent", "IntentCode", "Priority", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES (1, 'Fallback general', NULL, NULL, 0, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL);

                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: prioriza pasos de diagnóstico, capturas o menús a revisar, y redacción sugerida solo si el agente debe contactar al cliente.' WHERE "Code" = 'howto';
                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: revisar logs, versión, mensaje de error exacto y pasos de reproducción antes de proponer solución.' WHERE "Code" = 'error';
                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: pedir aclaración al cliente si el caso no encaja en consulta o error.' WHERE "Code" = 'otro';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "rag_prompt_selection_rules");
            migrationBuilder.DropTable(name: "urgency_profiles");
            migrationBuilder.DropTable(name: "rag_prompt_templates");

            migrationBuilder.DropColumn(
                name: "ResponseInstructions",
                table: "ticket_intents");
        }
    }
}
