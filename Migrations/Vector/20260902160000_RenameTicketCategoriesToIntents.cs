using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260902160000_RenameTicketCategoriesToIntents")]
    public partial class RenameTicketCategoriesToIntents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_category_assignments_ticket_categories_CategoryId",
                table: "ticket_category_assignments");

            migrationBuilder.RenameTable(
                name: "ticket_categories",
                newName: "ticket_intents");

            migrationBuilder.RenameTable(
                name: "ticket_category_assignments",
                newName: "ticket_intent_assignments");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "ticket_intent_assignments",
                newName: "IntentId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_categories_Code",
                table: "ticket_intents",
                newName: "IX_ticket_intents_Code");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_categories_SortOrder",
                table: "ticket_intents",
                newName: "IX_ticket_intents_SortOrder");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_category_assignments_CategoryId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_intent_assignments_IntentId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_category_assignments_TicketId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_intent_assignments_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_category_assignments_TicketId_CategoryId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_intent_assignments_TicketId_IntentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_intent_assignments_ticket_intents_IntentId",
                table: "ticket_intent_assignments",
                column: "IntentId",
                principalTable: "ticket_intents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ticket_intent_assignments_ticket_intents_IntentId",
                table: "ticket_intent_assignments");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_intent_assignments_TicketId_IntentId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_category_assignments_TicketId_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_intent_assignments_TicketId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_category_assignments_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_intent_assignments_IntentId",
                table: "ticket_intent_assignments",
                newName: "IX_ticket_category_assignments_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_intents_SortOrder",
                table: "ticket_intents",
                newName: "IX_ticket_categories_SortOrder");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_intents_Code",
                table: "ticket_intents",
                newName: "IX_ticket_categories_Code");

            migrationBuilder.RenameColumn(
                name: "IntentId",
                table: "ticket_intent_assignments",
                newName: "CategoryId");

            migrationBuilder.RenameTable(
                name: "ticket_intent_assignments",
                newName: "ticket_category_assignments");

            migrationBuilder.RenameTable(
                name: "ticket_intents",
                newName: "ticket_categories");

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_category_assignments_ticket_categories_CategoryId",
                table: "ticket_category_assignments",
                column: "CategoryId",
                principalTable: "ticket_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
