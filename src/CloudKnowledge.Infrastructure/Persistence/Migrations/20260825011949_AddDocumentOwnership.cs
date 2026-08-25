using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_owner_user_id",
                table: "documents",
                column: "owner_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_user_accounts_owner_user_id",
                table: "documents",
                column: "owner_user_id",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_user_accounts_owner_user_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_owner_user_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "documents");
        }
    }
}
