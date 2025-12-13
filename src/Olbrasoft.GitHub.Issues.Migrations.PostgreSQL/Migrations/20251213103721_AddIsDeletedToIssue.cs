using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_issues_is_deleted",
                table: "issues",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_issues_is_deleted",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "issues");
        }
    }
}
