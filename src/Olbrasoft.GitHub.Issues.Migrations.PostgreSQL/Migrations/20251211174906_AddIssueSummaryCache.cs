using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueSummaryCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "czech_summary",
                table: "issues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "summary_cached_at",
                table: "issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_provider",
                table: "issues",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "czech_summary",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "summary_cached_at",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "summary_provider",
                table: "issues");
        }
    }
}
