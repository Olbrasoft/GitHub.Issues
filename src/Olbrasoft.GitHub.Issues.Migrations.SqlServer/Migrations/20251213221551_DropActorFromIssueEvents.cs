using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DropActorFromIssueEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TranslatedTexts");

            migrationBuilder.DropColumn(
                name: "EnglishName",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "NativeName",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "TwoLetterISOCode",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "IssueEvents");

            migrationBuilder.DropColumn(
                name: "ActorLogin",
                table: "IssueEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TranslatedTexts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnglishName",
                table: "Languages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NativeName",
                table: "Languages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoLetterISOCode",
                table: "Languages",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorId",
                table: "IssueEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorLogin",
                table: "IssueEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 1029,
                columns: new[] { "EnglishName", "NativeName", "TwoLetterISOCode" },
                values: new object[] { "Czech (Czechia)", "čeština (Česko)", "cs" });

            migrationBuilder.UpdateData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 1031,
                columns: new[] { "EnglishName", "NativeName", "TwoLetterISOCode" },
                values: new object[] { "German (Germany)", "Deutsch (Deutschland)", "de" });

            migrationBuilder.UpdateData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 1033,
                columns: new[] { "EnglishName", "NativeName", "TwoLetterISOCode" },
                values: new object[] { "English (United States)", "English (United States)", "en" });
        }
    }
}
