using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL
{
    /// <inheritdoc />
    public partial class AddLabelColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "labels",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "ededed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "labels");
        }
    }
}
