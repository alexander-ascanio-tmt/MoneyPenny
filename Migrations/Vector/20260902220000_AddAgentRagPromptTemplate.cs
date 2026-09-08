using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;
using MoneyPenny.Services.Rag;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260902220000_AddAgentRagPromptTemplate")]
    public partial class AddAgentRagPromptTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var seededAt = new DateTime(2026, 9, 2, 22, 0, 0, DateTimeKind.Utc);

            var agentSystemPrompt = """
                Eres un asistente interno de soporte técnico de Telematel. Tu audiencia es el agente de soporte, no el cliente.
                Redactas en español una guía operativa con pasos concretos para diagnosticar y resolver el ticket.
                Usa únicamente la información del contexto proporcionado.
                Estructura la guía con: resumen del problema, hipótesis, pasos de diagnóstico, pasos de resolución, datos a pedir al cliente si faltan, y riesgos o escalado si aplica.
                No redactes el mensaje final para el cliente; el agente decidirá qué comunicar.
                Puedes citar tickets de referencia del contexto como uso interno.
                Si no hay información suficiente, indícalo y lista qué debe verificar el agente.
                """.Replace("'", "''");

            var agentUserPrompt = """
                Ticket actual (#{{ticketNumber}}) — comentario #1 indexado:
                {{currentTicketComment}}

                Contexto recuperado (tickets con problema similar; comentarios posteriores al #1 del cliente):
                {{context}}

                Pregunta:
                {{question}}

                Instrucciones:
                - El comentario #1 es lo que escribió el cliente; la guía es para el agente que atiende el ticket.
                - El contexto incluye cómo se resolvieron casos similares; úsalo como referencia interna.
                - Prioriza acciones inmediatas si hay impacto operativo (bloqueo, no puede facturar, etc.).
                - No inventes pasos ni datos sin apoyo en el contexto.
                """.Replace("'", "''");

            var agentQuestion = RagOrchestrator.AgentGenerationQuestion.Replace("'", "''");

            migrationBuilder.Sql(
                $"""
                INSERT INTO rag_prompt_templates ("Code", "Name", "SystemPrompt", "UserPromptTemplate", "GenerationQuestion", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT 'agent', 'Guía para agente (urgente)', '{agentSystemPrompt}', '{agentUserPrompt}', '{agentQuestion}', 20, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL
                WHERE NOT EXISTS (SELECT 1 FROM rag_prompt_templates WHERE "Code" = 'agent');

                UPDATE urgency_profiles
                SET "ResponseInstructions" = 'Ticket urgente: prioriza diagnóstico rápido, impacto en operación del cliente y escalado si no hay solución inmediata en el contexto.',
                    "UpdatedAt" = TIMESTAMPTZ '{seededAt:O}'
                WHERE "Code" = 'urgent';

                UPDATE urgency_profiles
                SET "ResponseInstructions" = '',
                    "UpdatedAt" = TIMESTAMPTZ '{seededAt:O}'
                WHERE "Code" = 'normal';

                INSERT INTO rag_prompt_selection_rules ("PromptTemplateId", "Name", "IsUrgent", "IntentCode", "Priority", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT t."Id", 'Ticket urgente → agente', TRUE, NULL, 100, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL
                FROM rag_prompt_templates t
                WHERE t."Code" = 'agent'
                  AND NOT EXISTS (
                      SELECT 1 FROM rag_prompt_selection_rules r
                      WHERE r."IsUrgent" = TRUE AND r."IntentCode" IS NULL AND r."Priority" = 100);

                INSERT INTO rag_prompt_selection_rules ("PromptTemplateId", "Name", "IsUrgent", "IntentCode", "Priority", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT t."Id", 'Ticket normal → cliente', FALSE, NULL, 50, TRUE, TIMESTAMPTZ '{seededAt:O}', NULL
                FROM rag_prompt_templates t
                WHERE t."Code" = 'default'
                  AND NOT EXISTS (
                      SELECT 1 FROM rag_prompt_selection_rules r
                      WHERE r."IsUrgent" = FALSE AND r."IntentCode" IS NULL AND r."Priority" = 50);

                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: prioriza pasos de diagnóstico, capturas o menús a revisar, y redacción sugerida solo si el agente debe contactar al cliente.' WHERE "Code" = 'howto';
                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: revisar logs, versión, mensaje de error exacto y pasos de reproducción antes de proponer solución.' WHERE "Code" = 'error';
                UPDATE ticket_intents SET "ResponseInstructions" = 'En la guía para el agente: pedir aclaración al cliente si el caso no encaja en consulta o error.' WHERE "Code" = 'otro';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM rag_prompt_selection_rules
                WHERE "Name" IN ('Ticket urgente → agente', 'Ticket normal → cliente');

                DELETE FROM rag_prompt_templates WHERE "Code" = 'agent';
                """);
        }
    }
}
