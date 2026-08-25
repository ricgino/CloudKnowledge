using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_issuer",
                table: "user_accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_subject",
                table: "user_accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_external_identity",
                table: "user_accounts",
                columns: new[] { "external_issuer", "external_subject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_accounts_external_identity",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "external_issuer",
                table: "user_accounts");

            migrationBuilder.DropColumn(
                name: "external_subject",
                table: "user_accounts");
        }
    }
}
