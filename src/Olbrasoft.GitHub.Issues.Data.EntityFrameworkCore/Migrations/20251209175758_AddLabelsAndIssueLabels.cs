using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelsAndIssueLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "summary_cz",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "summary_cz_embedding",
                table: "issues");

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issue_labels",
                columns: table => new
                {
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    label_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_labels", x => new { x.issue_id, x.label_id });
                    table.ForeignKey(
                        name: "FK_issue_labels_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issue_labels_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issue_labels_label_id",
                table: "issue_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "IX_labels_name",
                table: "labels",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_labels");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.AddColumn<string>(
                name: "summary_cz",
                table: "issues",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "summary_cz_embedding",
                table: "issues",
                type: "vector(1536)",
                nullable: true);
        }
    }
}
