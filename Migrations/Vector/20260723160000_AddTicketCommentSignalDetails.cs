using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoneyPenny.Data;

#nullable disable

namespace MoneyPenny.Migrations.Vector
{
    [DbContext(typeof(VectorDbContext))]
    [Migration("20260723160000_AddTicketCommentSignalDetails")]
    public partial class AddTicketCommentSignalDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageBoxDetail",
                table: "ticket_comment_signals",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentDetail",
                table: "ticket_comment_signals",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageBoxDetail",
                table: "ticket_comment_signals");

            migrationBuilder.DropColumn(
                name: "AttachmentDetail",
                table: "ticket_comment_signals");
        }
    }
}
