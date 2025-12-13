using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RenameToCachedTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_translated_texts_issues_issue_id",
                table: "TranslatedTexts");

            migrationBuilder.DropForeignKey(
                name: "fk_translated_texts_languages_language_id",
                table: "TranslatedTexts");

            migrationBuilder.DropForeignKey(
                name: "fk_translated_texts_text_types_text_type_id",
                table: "TranslatedTexts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_translated_texts",
                table: "TranslatedTexts");

            migrationBuilder.RenameTable(
                name: "TranslatedTexts",
                newName: "cached_texts");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "cached_texts",
                newName: "cached_at");

            migrationBuilder.RenameIndex(
                name: "ix_translated_texts_text_type_id",
                table: "cached_texts",
                newName: "ix_cached_texts_text_type_id");

            migrationBuilder.RenameIndex(
                name: "ix_translated_texts_issue_id",
                table: "cached_texts",
                newName: "ix_cached_texts_issue_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_cached_texts",
                table: "cached_texts",
                columns: new[] { "language_id", "text_type_id", "issue_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_cached_texts_issues_issue_id",
                table: "cached_texts",
                column: "issue_id",
                principalTable: "issues",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_cached_texts_languages_language_id",
                table: "cached_texts",
                column: "language_id",
                principalTable: "languages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_cached_texts_text_types_text_type_id",
                table: "cached_texts",
                column: "text_type_id",
                principalTable: "text_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cached_texts_issues_issue_id",
                table: "cached_texts");

            migrationBuilder.DropForeignKey(
                name: "fk_cached_texts_languages_language_id",
                table: "cached_texts");

            migrationBuilder.DropForeignKey(
                name: "fk_cached_texts_text_types_text_type_id",
                table: "cached_texts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_cached_texts",
                table: "cached_texts");

            migrationBuilder.RenameTable(
                name: "cached_texts",
                newName: "TranslatedTexts");

            migrationBuilder.RenameColumn(
                name: "cached_at",
                table: "TranslatedTexts",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_cached_texts_text_type_id",
                table: "TranslatedTexts",
                newName: "ix_translated_texts_text_type_id");

            migrationBuilder.RenameIndex(
                name: "ix_cached_texts_issue_id",
                table: "TranslatedTexts",
                newName: "ix_translated_texts_issue_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_translated_texts",
                table: "TranslatedTexts",
                columns: new[] { "language_id", "text_type_id", "issue_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_translated_texts_issues_issue_id",
                table: "TranslatedTexts",
                column: "issue_id",
                principalTable: "issues",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_translated_texts_languages_language_id",
                table: "TranslatedTexts",
                column: "language_id",
                principalTable: "languages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_translated_texts_text_types_text_type_id",
                table: "TranslatedTexts",
                column: "text_type_id",
                principalTable: "text_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
