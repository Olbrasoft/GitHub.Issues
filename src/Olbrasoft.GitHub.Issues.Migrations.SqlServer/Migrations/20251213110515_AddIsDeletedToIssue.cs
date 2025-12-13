using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Issues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_IsDeleted",
                table: "Issues",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Issues_IsDeleted",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Issues");
        }
    }
}
