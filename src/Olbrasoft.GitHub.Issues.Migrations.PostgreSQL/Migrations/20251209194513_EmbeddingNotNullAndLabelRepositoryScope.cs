using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class EmbeddingNotNullAndLabelRepositoryScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_labels_name",
                table: "labels");

            migrationBuilder.AddColumn<int>(
                name: "repository_id",
                table: "labels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "issues",
                type: "vector(1536)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_labels_repository_id_name",
                table: "labels",
                columns: new[] { "repository_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_labels_repositories_repository_id",
                table: "labels",
                column: "repository_id",
                principalTable: "repositories",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_labels_repositories_repository_id",
                table: "labels");

            migrationBuilder.DropIndex(
                name: "IX_labels_repository_id_name",
                table: "labels");

            migrationBuilder.DropColumn(
                name: "repository_id",
                table: "labels");

            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "issues",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)");

            migrationBuilder.CreateIndex(
                name: "IX_labels_name",
                table: "labels",
                column: "name",
                unique: true);
        }
    }
}
