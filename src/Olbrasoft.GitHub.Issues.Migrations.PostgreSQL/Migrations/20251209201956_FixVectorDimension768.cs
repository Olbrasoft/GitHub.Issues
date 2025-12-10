using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class FixVectorDimension768 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "issues",
                type: "vector(768)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "title_embedding",
                table: "issues",
                type: "vector(1536)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(768)");
        }
    }
}
