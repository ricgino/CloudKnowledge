using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace CloudKnowledge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChunkEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_chunk_embeddings",
                columns: table => new
                {
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunk_embeddings", x => x.chunk_id);
                    table.ForeignKey(
                        name: "FK_document_chunk_embeddings_document_chunks_chunk_id",
                        column: x => x.chunk_id,
                        principalTable: "document_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunk_embeddings_document_id",
                table: "document_chunk_embeddings",
                column: "document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunk_embeddings");
        }
    }
}
