using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CloudKnowledgeDbContext))]
[Migration("20260831002000_AddDocumentChunkSearchVector")]
public partial class AddDocumentChunkSearchVector
    : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE document_chunks
            ADD COLUMN search_vector tsvector
            GENERATED ALWAYS AS (
                to_tsvector('simple'::regconfig, coalesce(content, ''::text))
            ) STORED;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX "IX_document_chunks_search_vector"
            ON document_chunks
            USING GIN (search_vector);
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "IX_document_chunks_search_vector";
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE document_chunks
            DROP COLUMN IF EXISTS search_vector;
            """);
    }
}
