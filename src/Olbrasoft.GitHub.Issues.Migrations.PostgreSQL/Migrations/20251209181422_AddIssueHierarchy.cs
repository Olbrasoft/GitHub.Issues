using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddIssueHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_issue_id",
                table: "issues",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_parent_issue_id",
                table: "issues",
                column: "parent_issue_id");

            migrationBuilder.AddForeignKey(
                name: "FK_issues_issues_parent_issue_id",
                table: "issues",
                column: "parent_issue_id",
                principalTable: "issues",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_issues_issues_parent_issue_id",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "IX_issues_parent_issue_id",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "parent_issue_id",
                table: "issues");
        }
    }
}
