using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DropIssueCacheColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CzechSummary",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "CzechTitle",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "CzechTitleCachedAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SummaryCachedAt",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SummaryProvider",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "TitleTranslationProvider",
                table: "Issues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CzechSummary",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CzechTitle",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CzechTitleCachedAt",
                table: "Issues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SummaryCachedAt",
                table: "Issues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryProvider",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleTranslationProvider",
                table: "Issues",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
