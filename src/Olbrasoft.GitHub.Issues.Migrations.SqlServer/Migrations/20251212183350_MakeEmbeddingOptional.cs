using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmbeddingOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Embedding",
                table: "Issues",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "vector(1024)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: Removed invalid defaultValue: "" - empty string is not valid for vector(1024)
            // This migration can only be rolled back if all Issues have non-null Embedding values
            migrationBuilder.AlterColumn<string>(
                name: "Embedding",
                table: "Issues",
                type: "vector(1024)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "vector(1024)",
                oldNullable: true);
        }
    }
}
