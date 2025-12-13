using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Embedding",
                table: "Issues",
                type: "vector(1024)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "vector(1024)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CultureName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NativeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TwoLetterISOCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TranslatedTexts",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "int", nullable: false),
                    TextTypeId = table.Column<int>(type: "int", nullable: false),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslatedTexts", x => new { x.LanguageId, x.TextTypeId, x.IssueId });
                    table.ForeignKey(
                        name: "FK_TranslatedTexts_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TranslatedTexts_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TranslatedTexts_TextTypes_TextTypeId",
                        column: x => x.TextTypeId,
                        principalTable: "TextTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Id", "CultureName", "EnglishName", "NativeName", "TwoLetterISOCode" },
                values: new object[,]
                {
                    { 1029, "cs-CZ", "Czech (Czechia)", "čeština (Česko)", "cs" },
                    { 1031, "de-DE", "German (Germany)", "Deutsch (Deutschland)", "de" },
                    { 1033, "en-US", "English (United States)", "English (United States)", "en" }
                });

            migrationBuilder.InsertData(
                table: "TextTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Title" },
                    { 2, "ListSummary" },
                    { 3, "DetailSummary" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_CultureName",
                table: "Languages",
                column: "CultureName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TextTypes_Name",
                table: "TextTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TranslatedTexts_IssueId",
                table: "TranslatedTexts",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_TranslatedTexts_TextTypeId",
                table: "TranslatedTexts",
                column: "TextTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslatedTexts");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "TextTypes");

            migrationBuilder.AlterColumn<string>(
                name: "Embedding",
                table: "Issues",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "vector(1024)");
        }
    }
}
