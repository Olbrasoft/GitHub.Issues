using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class DropActorFromIssueEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translated_texts");

            migrationBuilder.DropColumn(
                name: "english_name",
                table: "languages");

            migrationBuilder.DropColumn(
                name: "native_name",
                table: "languages");

            migrationBuilder.DropColumn(
                name: "two_letter_iso_code",
                table: "languages");

            migrationBuilder.DropColumn(
                name: "actor_id",
                table: "issue_events");

            migrationBuilder.DropColumn(
                name: "actor_login",
                table: "issue_events");

            migrationBuilder.CreateTable(
                name: "TranslatedTexts",
                columns: table => new
                {
                    language_id = table.Column<int>(type: "integer", nullable: false),
                    text_type_id = table.Column<int>(type: "integer", nullable: false),
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_issue_id",
                table: "TranslatedTexts",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_text_type_id",
                table: "TranslatedTexts",
                column: "text_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslatedTexts");

            migrationBuilder.AddColumn<string>(
                name: "english_name",
                table: "languages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "native_name",
                table: "languages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "two_letter_iso_code",
                table: "languages",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "actor_id",
                table: "issue_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_login",
                table: "issue_events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

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

            migrationBuilder.UpdateData(
                table: "languages",
                keyColumn: "id",
                keyValue: 1029,
                columns: new[] { "english_name", "native_name", "two_letter_iso_code" },
                values: new object[] { "Czech (Czechia)", "čeština (Česko)", "cs" });

            migrationBuilder.UpdateData(
                table: "languages",
                keyColumn: "id",
                keyValue: 1031,
                columns: new[] { "english_name", "native_name", "two_letter_iso_code" },
                values: new object[] { "German (Germany)", "Deutsch (Deutschland)", "de" });

            migrationBuilder.UpdateData(
                table: "languages",
                keyColumn: "id",
                keyValue: 1033,
                columns: new[] { "english_name", "native_name", "two_letter_iso_code" },
                values: new object[] { "English (United States)", "English (United States)", "en" });

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_issue_id",
                table: "translated_texts",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_translated_texts_text_type_id",
                table: "translated_texts",
                column: "text_type_id");
        }
    }
}
