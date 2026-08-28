using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_team_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_owner_team_id",
                table: "documents",
                column: "owner_team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_teams_owner_team_id",
                table: "documents",
                column: "owner_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_teams_owner_team_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_owner_team_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "owner_team_id",
                table: "documents");
        }
    }
}
