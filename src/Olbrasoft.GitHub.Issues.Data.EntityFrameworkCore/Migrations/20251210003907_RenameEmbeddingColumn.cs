using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmbeddingColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "title_embedding",
                table: "issues",
                newName: "embedding");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "embedding",
                table: "issues",
                newName: "title_embedding");
        }
    }
}
