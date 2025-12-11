using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class MakeEmbeddingNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issue_labels_labels_label_id",
                table: "issue_labels");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "issues",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)");

            migrationBuilder.AddForeignKey(
                name: "FK_issue_labels_labels_label_id",
                table: "issue_labels",
                column: "label_id",
                principalTable: "labels",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issue_labels_labels_label_id",
                table: "issue_labels");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "issues",
                type: "vector(768)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_issue_labels_labels_label_id",
                table: "issue_labels",
                column: "label_id",
                principalTable: "labels",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
