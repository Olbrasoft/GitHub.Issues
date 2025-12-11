using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmbeddingRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete related data for issues without embeddings (FK constraints)
            migrationBuilder.Sql(@"
                DELETE FROM issue_events WHERE issue_id IN (SELECT id FROM issues WHERE embedding IS NULL);
                DELETE FROM issue_labels WHERE issue_id IN (SELECT id FROM issues WHERE embedding IS NULL);
                DELETE FROM issues WHERE embedding IS NULL;
            ");

            // Now make the column NOT NULL (no rows with NULL exist)
            migrationBuilder.AlterColumn<byte[]>(
                name: "embedding",
                table: "issues",
                type: "varbinary(max)",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "embedding",
                table: "issues",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");
        }
    }
}
