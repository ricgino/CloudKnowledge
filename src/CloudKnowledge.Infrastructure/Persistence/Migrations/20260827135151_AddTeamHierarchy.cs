using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_team_id",
                table: "teams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_teams_parent_team_id",
                table: "teams",
                column: "parent_team_id");

            migrationBuilder.AddForeignKey(
                name: "FK_teams_teams_parent_team_id",
                table: "teams",
                column: "parent_team_id",
                principalTable: "teams",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teams_teams_parent_team_id",
                table: "teams");

            migrationBuilder.DropIndex(
                name: "ix_teams_parent_team_id",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "parent_team_id",
                table: "teams");
        }
    }
}
