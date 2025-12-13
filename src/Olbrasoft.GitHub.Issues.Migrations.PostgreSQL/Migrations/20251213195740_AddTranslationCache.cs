using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    culture_name = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    english_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    native_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    two_letter_iso_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_text_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translated_texts",
                columns: table => new
                {
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    text_type_id = table.Column<int>(type: "integer", nullable: false),
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_translated_texts", x => new { x.language_id, x.text_type_id, x.issue_id });
                    table.ForeignKey(
                        name: "fk_translated_texts_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_translated_texts_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_translated_texts_text_types_text_type_id",
                        column: x => x.text_type_id,
                        principalTable: "text_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "languages",
                columns: new[] { "id", "culture_name", "english_name", "native_name", "two_letter_iso_code" },
                values: new object[,]
                {
                    { 1029, "cs-CZ", "Czech (Czechia)", "čeština (Česko)", "cs" },
                    { 1031, "de-DE", "German (Germany)", "Deutsch (Deutschland)", "de" },
                    { 1033, "en-US", "English (United States)", "English (United States)", "en" }
                });

            migrationBuilder.InsertData(
                table: "text_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Title" },
                    { 2, "ListSummary" },
                    { 3, "DetailSummary" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_languages_culture_name",
                table: "languages",
                column: "culture_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_text_types_name",
                table: "text_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_issue_id",
                table: "translated_texts",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_text_type_id",
                table: "translated_texts",
                column: "text_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translated_texts");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "text_types");
        }
    }
}
